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
        public static async Task<bool> EnsureRenewedAsync(string staleToken, CancellationToken cancellationToken)
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
                    return !string.IsNullOrEmpty(current);
                }

                var refreshToken = await GetRefreshTokenAsync().ConfigureAwait(false);

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                using var response = await Client.Value
                    .PostAsJsonAsync("api/Auth/refresh", new { refreshToken }, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"TokenRenewal: the server refused to renew ({(int)response.StatusCode}).");
                    return false;
                }

                var pair = await response.Content
                    .ReadFromJsonAsync<TokenPair>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (pair is null || string.IsNullOrEmpty(pair.Token))
                {
                    return false;
                }

                await StoreAsync(pair.Token, pair.RefreshToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // ⚠️ No signal is NOT an expired session. A driver goes through dead spots
                // several times a route; being signed out by one would be far worse than the
                // request simply failing and being retried.
                System.Diagnostics.Debug.WriteLine($"TokenRenewal: could not reach the server. {ex.Message}");
                return false;
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
