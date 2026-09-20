using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Raphael.Driver.Exceptions;
using Raphael.Driver.Models;
using Raphael.Driver.Views;

namespace Raphael.Driver.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        //private string URI = App.Configuration["ApiAddress:ApiTest"];
        private readonly IGpsService _gpsService;

        /// <summary>
        /// ⚠️ The <see cref="HttpClient"/> is now taken from dependency injection instead of
        /// being built here with a hard-coded address. The old constructor overwrote what
        /// MauiProgram had just configured, so changing the address there moved every call in
        /// the application EXCEPT signing in -- the worst possible half-move, because the
        /// office would be working in one database while the drivers authenticated against
        /// another and nothing would look broken. Taking the injected client also means
        /// signing in finally passes through ClientVersionHandler.
        /// </summary>
        public AuthService(HttpClient httpClient, IGpsService gpsService)
        {
            _httpClient = httpClient;
            _gpsService = gpsService;
        }

        /// <remarks>
        /// ⚠️ Nothing in here blocks a thread, and that is the whole point of the rewrite.
        /// The previous version was <c>void</c> and ran on the UI thread, where it called
        /// <c>GetRefreshTokenAsync().GetAwaiter().GetResult()</c>. Secure storage resumed onto
        /// the UI thread, the UI thread was sitting inside <c>GetResult()</c> waiting for it,
        /// and the application froze — every time, on both environments, with no way out but
        /// killing it. Signing out is the one action that must never be able to trap a driver.
        /// </remarks>
        public async Task LogoutAsync()
        {
            // ⚠️ Notifications go down BEFORE the session is wiped: both the call that forgets
            // this device on the server and the hub connection need the token that is about to
            // disappear. Phones are handed over between shifts, and a device left registered
            // keeps receiving the previous driver's notifications — trips that are not theirs.
            await StopNotificationsAsync().ConfigureAwait(false);

            // Tell the server before the credential is gone. The revocation itself is not
            // awaited: a phone with no signal must still be able to sign out.
            var refreshToken = await Auth.TokenRenewal.GetRefreshTokenAsync().ConfigureAwait(false);
            _ = Auth.TokenRenewal.RevokeAsync(refreshToken);
            Auth.TokenRenewal.Forget();

            // ⚠️ Preferences.Clear() would also erase which server this phone talks to, so a
            // driver who signs out loses the address support just walked them through setting
            // -- silently falling back to the compiled default, which is the thing the support
            // call was trying to get away from.
            Configuration.ApiEnvironment.PreserveAcross(Preferences.Clear);

            // Stop GPS tracking
            if (_gpsService.IsTracking)
            {
                Debug.WriteLine("Logging out. Stopping GPS tracking service.");
                _gpsService.StopTracking();
            }

            // Back to the UI thread explicitly: everything above ran with
            // ConfigureAwait(false), so by here there is no guarantee of being on it, and
            // touching Shell from a background thread is its own crash.
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // The flyout does not close on its own when the route changes, so signing out
                // left the menu hanging open over the login page.
                Shell.Current.FlyoutIsPresented = false;

                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            });
        }

        private static async Task StopNotificationsAsync()
        {
            try
            {
                NotificationRouter.Clear();

                var session = ServiceHelper.GetService<NotificationSessionService>();

                if (session is null)
                    return;

                // Awaited rather than fired and forgotten: Preferences.Clear() runs right
                // after this and the API call still needs the token. The timeout means a
                // phone with no signal cannot leave a driver unable to sign out — and
                // WaitAsync, not Wait, so the five seconds are spent waiting rather than
                // holding the interface frozen.
                await session.StopAsync()
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Signing out must always succeed. A device left registered on the server is
                // a problem; a driver who cannot sign out is a worse one.
                Debug.WriteLine($"AuthService: could not stop notifications. {ex.Message}");
            }
        }
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            if (_httpClient.BaseAddress == null)
            {             
                // If BaseAddress could not be set in the constructor.
                throw new ApiException("The authentication service configuration is incorrect (invalid base URL).");
            }
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

                if (!response.IsSuccessStatusCode)
                {
                    //throw await CreateApiException(response, "Authentication error");
                    //throw await CreateApiException(response, $"Authentication error ({(int)response.StatusCode})");
                    return new LoginResponse { IsSuccess = false, Message = "Login Failed: Invalid credentials. Incorrect username or password." };
                }
                // Trying to deserialize, could fail if the JSON is not as expected.
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (loginResponse == null)
                {
                    throw new ApiException("Unexpected response from the server after login.");
                }
                return loginResponse;

                //return await response.Content.ReadFromJsonAsync<LoginResponse>();               
            }             
            catch (HttpRequestException ex) // Network errors, DNS, server not available, etc.
            {             
                throw new ApiException("Server connection error. Check your internet connection.", ex);
            }
            catch (JsonException ex) // Error deserializing JSON response
            {
                throw new ApiException("Error processing server response.", ex);
            }
       
            catch (Exception ex)
            {
                throw new ApiException("An unexpected error occurred during login.", ex);
            }

        }

        private async Task<ApiException> CreateApiException(HttpResponseMessage response, string context)
        {
            try
            {
                // Try to read ProblemDetails if available (common in ASP.NET Core APIs)
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                return new ApiException(
                    message: $"{context}: {problemDetails?.Title ?? "Unknown error"}",
                    statusCode: response.StatusCode,
                    details: problemDetails?.Detail);
            }
            catch (JsonException) // If the content is not ProblemDetails or is not JSON
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                // Limit the length of the errorContent so as not to show too much in the UI
                if (errorContent.Length > 200) errorContent = errorContent.Substring(0, 200) + "...";
                return new ApiException(
                    message: $"{context}",
                    statusCode: response.StatusCode,
                    details: string.IsNullOrWhiteSpace(errorContent) ? "The server did not provide additional details." : errorContent);
            }
            catch (Exception ex) // Another error processing the error response
            {
                return new ApiException(
                   message: $"{context}: Could not process the server error response.",
                   statusCode: response.StatusCode,
                   details: ex.Message);
            }           
        }
    }
}
