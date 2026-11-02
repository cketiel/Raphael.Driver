using System.Net.Http.Json;
using System.Diagnostics;
using Raphael.Driver.Models;

namespace Raphael.Driver.Services
{
    public class RunService : IRunService
    {
        private readonly HttpClient _httpClient;

        public RunService(HttpClient httpClient)
        {
            //var baseUrl = Preferences.Get("ApiBaseUrl", "https://krasnovbw-001-site1.rtempurl.com/");
            //baseUrl = "https://krasnovbw-001-site1.rtempurl.com/";
            //_httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

            _httpClient = httpClient;            
        }

        public async Task<VehicleRoute> GetActiveRunByDriverIdAsync(int driverId)
        {
            // RE-062: the last answer is kept on the phone, so a driver who opens the app without
            // coverage still sees their run and their day, read from the phone's copy.
            var key = $"ActiveRun:{driverId}";
            try
            {
                var response = await _httpClient.GetAsync($"api/runs/driver/{driverId}");
                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        Preferences.Remove(key);
                        return null;
                    }
                    var json = await response.Content.ReadAsStringAsync();
                    Preferences.Set(key, json);
                    return System.Text.Json.JsonSerializer.Deserialize<VehicleRoute>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Active run from the phone's copy: {ex.Message}");
            }

            try
            {
                var copy = Preferences.Get(key, string.Empty);
                return string.IsNullOrEmpty(copy) ? null
                    : System.Text.Json.JsonSerializer.Deserialize<VehicleRoute>(copy, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }
}
