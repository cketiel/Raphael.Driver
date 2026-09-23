using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raphael.Driver.Services
{
    /// <summary>Asking the dispatch office to call back, instead of phoning it.</summary>
    public interface ICallRequestService
    {
        Task<DriverCallRequestDto?> GetCurrentAsync(CancellationToken cancellationToken = default);

        /// <summary>Opens a request, or reminds the office of the one already open.</summary>
        Task<DriverCallRequestDto?> RequestAsync(CreateCallRequestDto request);

        /// <summary>"I can talk now", after the office tried to call.</summary>
        Task<DriverCallRequestDto?> MarkAvailableAsync(int requestId);

        Task<DriverCallRequestDto?> CancelAsync(int requestId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every method answers null when the server could not be reached or refused. The caller reads
    /// that as "not sent" and offers the office's phone instead: a broken request button must never
    /// leave a driver with no way to reach the office.
    /// </remarks>
    public class CallRequestService : ICallRequestService
    {
        private const string Endpoint = "api/driver/call-requests";

        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _http;

        public CallRequestService(HttpClient http)
        {
            _http = http;
        }

        public Task<DriverCallRequestDto?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            SendAsync(() => _http.GetAsync($"{Endpoint}/current", cancellationToken));

        public Task<DriverCallRequestDto?> RequestAsync(CreateCallRequestDto request) =>
            SendAsync(() => _http.PostAsJsonAsync(Endpoint, request));

        public Task<DriverCallRequestDto?> MarkAvailableAsync(int requestId) =>
            SendAsync(() => _http.PostAsync($"{Endpoint}/{requestId}/available", null));

        public Task<DriverCallRequestDto?> CancelAsync(int requestId) =>
            SendAsync(() => _http.PostAsync($"{Endpoint}/{requestId}/cancel", null));

        private static async Task<DriverCallRequestDto?> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            try
            {
                var response = await send();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"CallRequestService: the server answered {(int)response.StatusCode}.");
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<DriverCallRequestDto>(Json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestService: {ex.Message}");
                return null;
            }
        }
    }
}
