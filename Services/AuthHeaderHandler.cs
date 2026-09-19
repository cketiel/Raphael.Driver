using System.Net;
using System.Net.Http.Headers;
using Raphael.Driver.Services.Auth;

namespace Raphael.Driver.Services;

/// <summary>
/// Puts the access token on every request, and renews it once on a 401 before giving up.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ What this replaces: on any 401 it deleted the token and sent the driver back to the sign-in
/// screen. That happened on a token that had simply aged out, which on a ten-hour token meant
/// once a shift — in the middle of a route, with a patient in the vehicle, and with no way back
/// except typing a password on a phone in a moving van. Renewing is the whole point of the
/// refresh token; the sign-in screen is now the last resort, not the first response.
/// </para>
/// </remarks>
public class AuthHeaderHandler : DelegatingHandler
{
    private static int _signOutInProgress;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var tokenSent = Preferences.Get("AuthToken", string.Empty);

        if (!string.IsNullOrEmpty(tokenSent))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSent);
        }

        // Buffered so the request survives being sent twice. Without this the retry would post
        // a stream that has already been read, and the failure would look like the server
        // rejecting an empty body.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(tokenSent))
        {
            return response;
        }

        var outcome = await TokenRenewal
            .EnsureRenewedAsync(tokenSent, cancellationToken)
            .ConfigureAwait(false);

        if (outcome == TokenRenewal.RenewalOutcome.Renewed)
        {
            response.Dispose();

            // Once, never in a loop: a 401 that survives a fresh token is not about the token.
            var retry = Clone(request);
            retry.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", Preferences.Get("AuthToken", string.Empty));

            return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        // ⚠️ Only SessionOver ends the session. Unavailable means nothing was decided — no
        // signal, a timeout, a server still starting up — and the request simply fails, which
        // the screen that made it can retry. Before this, any failure to renew signed the
        // driver out: on DEV, which has no Always On and takes a minute and a half to wake,
        // that meant signing in and being thrown straight back to the sign-in screen.
        //
        // ⚠️ Console.WriteLine and not Debug.WriteLine, and this is the reason the last three
        // faults took a build each to find: Debug.WriteLine is [Conditional("DEBUG")], so
        // every diagnostic line in this application writes nothing at all in the Release APK
        // that testers and drivers actually run. Being signed out unexpectedly is the single
        // hardest thing to diagnose after the fact -- the evidence is gone with the session --
        // so this one line survives into Release, tagged for grepping in logcat.
        //
        Console.WriteLine(
            $"RAPHAEL-SESSION: 401 on {request.Method} {request.RequestUri?.PathAndQuery}, " +
            $"renewal outcome {outcome}. " +
            (outcome == TokenRenewal.RenewalOutcome.SessionOver
                ? "Signing out."
                : "Leaving the session alone; the request just fails."));

        if (outcome == TokenRenewal.RenewalOutcome.SessionOver)
        {
            SignOut();
        }

        return response;
    }

    /// <summary>
    /// The session really is over. Once, however many requests fail together.
    /// </summary>
    private static void SignOut()
    {
        if (Interlocked.Exchange(ref _signOutInProgress, 1) != 0)
        {
            return;
        }

        Preferences.Remove("AuthToken");
        TokenRenewal.Forget();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current?.CurrentPage is not Views.LoginPage)
                {
                    await Shell.Current.GoToAsync("///LoginPage");
                }
            }
            finally
            {
                Interlocked.Exchange(ref _signOutInProgress, 0);
            }
        });
    }

    /// <summary>
    /// A request message cannot be sent twice. The content object is reused rather than copied:
    /// it was buffered above and is safe to read again.
    /// </summary>
    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            Content = request.Content
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object>(option.Key), option.Value);
        }

        return clone;
    }
}
