using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raphael.Driver.Services
{
    /// <summary>The driver's own account, from Settings › Account.</summary>
    public interface IAccountService
    {
        /// <summary>Changes the driver's password; the current one is required. Null when done, else a sentence for the driver.</summary>
        Task<string?> ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword);
    }

    public sealed class AccountService : IAccountService
    {
        private readonly HttpClient _http;

        public AccountService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string?> ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
        {
            if (!int.TryParse(Preferences.Get("UserId", string.Empty), out var userId))
                return "Sign in again to change your password.";
            try
            {
                // The server answers 403 to any id but the caller's own: the id only names who is asking.
                var response = await _http.PutAsJsonAsync("api/users/change-password", new
                {
                    userId,
                    currentPassword,
                    newPassword,
                    confirmPassword
                });
                if (response.IsSuccessStatusCode) return null;
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } message) return message;
                }
                catch (JsonException)
                {
                }
                return "The password was not changed. Try again.";
            }
            catch (Exception ex)
            {
                // Never the passwords: only what kind of failure it was.
                Debug.WriteLine($"Change password: {ex.GetType().Name}");
                return "No connection. Try again when you have signal.";
            }
        }
    }
}
