using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Raphael.Driver.Services
{
    public class NotificationApiService : INotificationApiService
    {
        private const string BaseRoute = "api/driver/notifications";

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly Offline.Outbox _outbox;

        public NotificationApiService(HttpClient httpClient, Offline.Outbox outbox)
        {
            _outbox = outbox;
            _httpClient = httpClient;
            _serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<List<NotificationDto>?> GetAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(BaseRoute, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Null, not an empty list. Reporting a refusal as "you have nothing" is how
                    // a misconfigured DriverRoleIds would hide itself for ever.
                    Console.WriteLine($"RAPHAEL-NOTIF: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                return JsonSerializer.Deserialize<List<NotificationDto>>(content, _serializerOptions)
                       ?? new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                // A failed inbox refresh must never take the app down: the driver still has a
                // schedule to run.
                Debug.WriteLine($"Exception in GetAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseRoute}/unread-count", cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return 0;

                var payload = await response.Content.ReadFromJsonAsync<UnreadCountResponse>(
                    _serializerOptions,
                    cancellationToken);

                return payload?.Count ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetUnreadCountAsync: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<NotificationDto>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseRoute}/signals", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Error fetching signals: {response.StatusCode}");
                    return new List<NotificationDto>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                return JsonSerializer.Deserialize<List<NotificationDto>>(content, _serializerOptions)
                       ?? new List<NotificationDto>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSignalsAsync: {ex.Message}");
                return new List<NotificationDto>();
            }
        }

        public async Task<bool> DeleteSignalAsync(Guid recipientRecordId, CancellationToken cancellationToken = default) =>
            (await _outbox.SendAsync(Offline.OutboxRequest.Empty("DELETE", $"{BaseRoute}/signals/{recipientRecordId}", "notification"))).Accepted;

        public Task<bool> MarkViewedAsync(Guid recipientRecordId, CancellationToken cancellationToken = default)
            => PostAsync($"{BaseRoute}/{recipientRecordId}/view", cancellationToken);

        public Task<bool> MarkUnviewedAsync(Guid recipientRecordId, CancellationToken cancellationToken = default)
            => PostAsync($"{BaseRoute}/{recipientRecordId}/unview", cancellationToken);

        public Task<bool> MarkAcknowledgedAsync(Guid recipientRecordId, CancellationToken cancellationToken = default)
            => PostAsync($"{BaseRoute}/{recipientRecordId}/acknowledge", cancellationToken);

        public Task<bool> MarkAllViewedAsync(CancellationToken cancellationToken = default)
            => PostAsync($"{BaseRoute}/read-all", cancellationToken);

        public async Task<bool> RegisterPushTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                // The endpoint takes the raw token as a JSON string body, not an object.
                var content = new StringContent(
                    JsonSerializer.Serialize(token),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("api/driver/push-token", content, cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in RegisterPushTokenAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearPushTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.DeleteAsync("api/driver/push-token", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in ClearPushTokenAsync: {ex.Message}");
                return false;
            }
        }

        // RE-062: marking read, unread or acknowledged is kept on the phone without coverage and sent later.
        private async Task<bool> PostAsync(string requestUri, CancellationToken cancellationToken) =>
            (await _outbox.SendAsync(Offline.OutboxRequest.Empty("POST", requestUri, "notification"))).Accepted;

        private sealed class UnreadCountResponse
        {
            public int Count { get; set; }
        }
    }
}
