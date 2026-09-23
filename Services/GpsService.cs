using Raphael.Driver.DTOs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Raphael.Driver.Services
{
    // The problem with GPS services (especially on Android) is that they are instantiated by the operating system and not directly by the MAUI dependency container in the traditional way.
    // For this to work correctly with the JWT interceptor, we will use a "Named HttpClient" in MauiProgram.cs and then retrieve it within each service.
    public class GpsService : IGpsService
    {
        private System.Timers.Timer _timer;
        private readonly HttpClient _httpClient;
        private int _idVehicleRoute;

        public bool IsTracking => _timer?.Enabled ?? false;

        public event Action<bool> IsTrackingChanged;

        public GpsService()
        {
            var factory = IPlatformApplication.Current.Services.GetRequiredService<IHttpClientFactory>();
            _httpClient = factory.CreateClient("GpsClient");

            /*_httpClient = new HttpClient();           
            //_httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/"); // Example for Android Emulator
            _httpClient.BaseAddress = new Uri("https://krasnovbw-001-site1.rtempurl.com/");*/
        }

        public void StartTracking(int idVehicleRoute)
        {
            if (IsTracking) return;

            _idVehicleRoute = idVehicleRoute;
            _timer = new System.Timers.Timer(60000); // 1 minuto
            _timer.Elapsed += async (s, e) => await SendLocationAsync();
            _timer.Start();
          
            Task.Run(SendLocationAsync);

            IsTrackingChanged?.Invoke(true);
        }

        public void StopTracking()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            IsTrackingChanged?.Invoke(false);
        }

        public Task SendNowAsync() => IsTracking ? SendLocationAsync() : Task.CompletedTask;

        private async Task SendLocationAsync()
        {
            try
            {
                var location = await GetCurrentLocationAsync();
                if (location == null) return;

                var gpsData = new GpsDataDto
                {
                    IdVehicleRoute = _idVehicleRoute,
                    DateTime = DateTime.UtcNow,
                    Speed = location.Speed ?? 0,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Direction = GetCardinalDirection(location.Course)
                };

                var response = await _httpClient.PostAsJsonAsync("api/Gps", gpsData);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Error sending GPS data: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in SendLocationAsync: {ex.Message}");
            }
        }

        public async Task<Location> GetCurrentLocationAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                return await Geolocation.Default.GetLocationAsync(request);
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // GPS is not supported on this device
                Debug.WriteLine($"Geolocation is not supported on this device: {fnsEx.Message}");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // GPS is not activated
                Debug.WriteLine($"The GPS is not activated: {fneEx.Message}");
            }
            catch (PermissionException pEx)
            {
                // You do not have the necessary permissions
                Debug.WriteLine($"You do not have geolocation permissions: {pEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to get location: {ex.Message}");
                //return null;
            }
            return null;
        }

        private string GetCardinalDirection(double? bearing)
        {
            if (!bearing.HasValue) return null;
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(bearing.Value % 360 / 45)];
        }
    }
}
