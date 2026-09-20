using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Raphael.Driver.Configuration;

namespace Raphael.Driver.Services.Auth
{
    /// <summary>
    /// Keeps the driver signed in across a shift: buys a new access token with the refresh
    /// token, once, however many requests discover the expiry together.
    /// </summary>
    public static class TokenRenewal
    {
        /// <summary>
        /// ⚠️ <see cref="SecureStorage"/> and not <c>Preferences</c>. A refresh token is a
        /// credential that lasts weeks; preferences are a plain file that anything with access
        /// to the device storage can read. It is also why signing out has to remove it
        /// explicitly — clearing preferences does not touch this.
        /// </summary>
        private const string RefreshTokenKey = "Raphael.Api.RefreshToken";

        /// <summary>
        /// One renewal at a time. The schedule page, the run service and the notification
        /// poller all discover the expiry within the same second; each renewal rotates the
        /// refresh token, so unserialised attempts would present one the server had just
        /// replaced — which the server reads as a stolen credential and answers by ending the
        /// session. Concurrency here would sign a driver out mid-route.
        /// </summary>
        private static readonly SemaphoreSlim Gate = new(1, 1);

        /// <summary>
        /// Its own client on purpose: this call must not pass through
        /// <see cref="AuthHeaderHandler"/>, or a failed renewal would try to renew itself.
        /// </summary>
        private static readonly Lazy<HttpClient> Client = new(() => new HttpClient
        {
            BaseAddress = new Uri(ApiEnvironment.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        });

        private sealed class TokenPair
        {
            public string Token { get; set; }
            public string RefreshToken { get; set; }
        }

        /// <summary>
        /// What came of trying to renew. Three outcomes and not a <c>bool</c>, because two of
        /// them used to be the same value and the difference is a driver's session.
        /// </summary>
        /// <remarks>
        /// ⚠️ "The server said no" and "I could not reach the server" both returned false, and
        /// the caller signed the driver out on false. So a timeout ended the session — right
        /// under a comment in this file saying that a dead spot is not an expired session. It
        /// showed up on DEV, which has no Always On and takes 89 to 100 seconds to wake: a
        /// renewal that landed during a cold start timed out and threw the driver back to the
        /// sign-in screen. On production, always warm, renewal answers at once and nobody ever
        /// saw it.
        /// </remarks>
        public enum RenewalOutcome
        {
            /// <summary>There is a token newer than the one that failed. Retry the request.</summary>
            Renewed,

            /// <summary>The server refused the refresh token. The session really is over.</summary>
            SessionOver,

            /// <summary>
            /// Nothing was decided: no signal, a timeout, or the server having a bad minute.
            /// The request fails and the session is left exactly as it was.
            /// </summary>
            Unavailable
        }

        /// <remarks>
        /// ⚠️ <c>ConfigureAwait(false)</c> is not decoration here. Without it the continuation
        /// comes back to the UI thread, and a caller that blocks on this from the UI thread
        /// deadlocks the application outright. That is exactly what signing out did: it froze
        /// the phone, with no way out but killing the app. The blocking call is gone too --
        /// both halves, because either one alone leaves the trap armed for the next caller.
        /// </remarks>
        public static async Task<string> GetRefreshTokenAsync()
        {
            try
            {
                return await SecureStorage.GetAsync(RefreshTokenKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Some devices refuse the keystore after an OS update or a restore. Losing the
                // refresh token costs one sign-in; throwing here would cost the whole session.
                System.Diagnostics.Debug.WriteLine($"TokenRenewal: secure storage unreadable. {ex.Message}");
                return null;
            }
        }

        public static async Task StoreAsync(string accessToken, string refreshToken)
        {
            Preferences.Set("AuthToken", accessToken ?? string.Empty);

            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                {
                    SecureStorage.Remove(RefreshTokenKey);
                }
                else
                {
                    await SecureStorage.SetAsync(RefreshTokenKey, refreshToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TokenRenewal: could not write secure storage. {ex.Message}");
            }
        }

        public static void Forget()
        {
            try
            {
                SecureStorage.Remove(RefreshTokenKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TokenRenewal: could not clear secure storage. {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the session holds an access token newer than <paramref name="staleToken"/>.
        /// </summary>
        /// <returns>False when the session is genuinely over and the driver must sign in.</returns>
        /// <summary>
        /// What a non-OK answer from <c>/api/Auth/refresh</c> means for the session. Pure, and
        /// separate from everything else, so it can be exercised case by case.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠️ <b>409 is not a failure.</b> The backend answers <c>rotated_recently</c> with
        /// Conflict precisely so a client does not send the driver to the sign-in screen —
        /// <c>AuthController.Refresh</c> says so in as many words: "401 tells a client to send
        /// the user back to the login screen, and this is the one failure where it must not".
        /// It means another request renewed with this same refresh token seconds ago. The
        /// gate catches that inside one process, but not when the other renewal finished
        /// after this one read the stored token.
        /// </para>
        /// <para>
        /// ⚠️ <b>Only an answer about the credential ends a session.</b> 401 and 403 are the
        /// server saying this refresh token is no good. A 500 because it is still starting, a
        /// 429 from the rate limiter, a 408 — none of those say anything about the session,
        /// and treating them as the end of one is what signed drivers out for a server hiccup
        /// they never saw.
        /// </para>
        /// </remarks>
        internal static RenewalOutcome Classify(HttpStatusCode status, bool newerTokenStored) => status switch
        {
            HttpStatusCode.OK => RenewalOutcome.Renewed,

            HttpStatusCode.Conflict => newerTokenStored
                ? RenewalOutcome.Renewed
                : RenewalOutcome.Unavailable,

            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RenewalOutcome.SessionOver,

            _ => RenewalOutcome.Unavailable
        };

        public static async Task<RenewalOutcome> EnsureRenewedAsync(
            string staleToken,
            CancellationToken cancellationToken)
        {
            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Somebody renewed while this request waited at the gate. Normal for all but
                // the first of a burst, and a success: the caller only needs a token that is
                // not the one that just failed.
                var current = Preferences.Get("AuthToken", string.Empty);

                if (!string.Equals(current, staleToken, StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(current)
                        ? RenewalOutcome.SessionOver
                        : RenewalOutcome.Renewed;
                }

                var refreshToken = await GetRefreshTokenAsync().ConfigureAwait(false);

                if (string.IsNullOrEmpty(refreshToken))
                {
                    // Nothing to renew with. Typing a password is the only way on from here.
                    return RenewalOutcome.SessionOver;
                }

                using var response = await Client.Value
                    .PostAsJsonAsync("api/Auth/refresh", new { refreshToken }, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // Whether another request already stored a token newer than the one that
                    // failed. Only 409 cares, but it is read here so Classify stays pure.
                    var newerTokenStored = !string.Equals(
                        Preferences.Get("AuthToken", string.Empty),
                        staleToken,
                        StringComparison.Ordinal);

                    var outcome = Classify(response.StatusCode, newerTokenStored);

                    // Console, not Debug: see the note in AuthHeaderHandler. This is the line
                    // that says whether an unexpected sign-out was the server refusing the
                    // credential or the server simply not being ready.
                    Console.WriteLine(
                        $"RAPHAEL-SESSION: renewal against {Client.Value.BaseAddress} answered " +
                        $"{(int)response.StatusCode} -> {outcome}.");

                    return outcome;
                }

                var pair = await response.Content
                    .ReadFromJsonAsync<TokenPair>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (pair is null || string.IsNullOrEmpty(pair.Token))
                {
                    // A 200 with nothing usable in it is the server misbehaving, not the
                    // session ending.
                    return RenewalOutcome.Unavailable;
                }

                await StoreAsync(pair.Token, pair.RefreshToken).ConfigureAwait(false);
                return RenewalOutcome.Renewed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // ⚠️ No signal is NOT an expired session. A driver goes through dead spots
                // several times a route; being signed out by one would be far worse than the
                // request simply failing and being retried.
                //
                // This comment was already here, and the code under it returned false, and
                // the caller signed the driver out on false. The comment was right and the
                // code did the opposite of what it said.
                Console.WriteLine(
                    $"RAPHAEL-SESSION: renewal against {Client.Value.BaseAddress} could not " +
                    $"reach the server ({ex.GetType().Name}: {ex.Message}). Session left alone.");
                return RenewalOutcome.Unavailable;
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        /// Tells the server to forget this session. Best effort, and never awaited by a sign-out:
        /// a phone with no signal must still be able to sign out.
        /// </summary>
        public static async Task RevokeAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                return;
            }

            try
            {
                using var _ = await Client.Value
                    .PostAsJsonAsync("api/Auth/logout", new { refreshToken })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TokenRenewal: sign-out was not delivered. {ex.Message}");
            }
        }
    }
}
