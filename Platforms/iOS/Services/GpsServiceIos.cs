using CoreLocation;
using Foundation;
using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;

// Alias ​​to avoid ambiguity with Microsoft.Maui.Devices.Sensors.Location
using IosLocation = CoreLocation.CLLocation;

namespace Raphael.Driver.Services
{
    public class GpsServiceIos : NSObject, IGpsService, ICLLocationManagerDelegate
    {
        private readonly HttpClient _httpClient;
        private CLLocationManager _locationManager;
        private int _idVehicleRoute;
        private bool _isTracking = false;

        public bool IsTracking => _isTracking;

        public event Action<bool> IsTrackingChanged;

        public GpsServiceIos()
        {
            // We get the client configured with the interceptor
            var factory = IPlatformApplication.Current.Services.GetRequiredService<IHttpClientFactory>();
            _httpClient = factory.CreateClient("GpsClient");

            /*_httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://krasnovbw-001-site1.rtempurl.com/")
            };*/
        }

        private void InitializeLocationManager()
        {
            if (_locationManager != null) return;

            _locationManager = new CLLocationManager();
            _locationManager.Delegate = this;
        }

        public void StartTracking(int vehicleRouteId)
        {
            if (_isTracking) return;

            InitializeLocationManager();
            _idVehicleRoute = vehicleRouteId;

            // --- CRITICAL SETTINGS FOR BACKGROUND ---
            _locationManager.PausesLocationUpdatesAutomatically = false;
            _locationManager.AllowsBackgroundLocationUpdates = true;
            _locationManager.DesiredAccuracy = CLLocation.AccuracyBest;
            _locationManager.DistanceFilter = CLLocation.AccuracyHundredMeters; 
            _locationManager.StartUpdatingLocation();

            _isTracking = true;
            Debug.WriteLine("iOS GPS Tracking Started");
            IsTrackingChanged?.Invoke(true);
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _locationManager?.StopUpdatingLocation();
            _isTracking = false;
            Debug.WriteLine("iOS GPS Tracking Stopped");
            IsTrackingChanged?.Invoke(false);
        }

        // This is the delegate method that the OS calls with new locations
        [Export("locationManager:didUpdateLocations:")]
        public void LocationsUpdated(CLLocationManager manager, IosLocation[] locations)
        {
            var lastLocation = locations.LastOrDefault();
            if (lastLocation == null) return;

            Debug.WriteLine($"New location received: Lat {lastLocation.Coordinate.Latitude}, Lon {lastLocation.Coordinate.Longitude}");

            // Convert native location to MAUI location to maintain common logic
            var mauiLocation = new Location(lastLocation.Coordinate.Latitude, lastLocation.Coordinate.Longitude);

            // Send the location to the server (without waiting, so as not to block the UI thread)
            _ = SendLocationAsync(mauiLocation, lastLocation.Speed, lastLocation.Course);
        }

        public async Task SendNowAsync()
        {
            if (!_isTracking)
                return;

            try
            {
                var location = await GetCurrentLocationAsync();

                if (location is null)
                    return;

                await SendLocationAsync(location, location.Speed ?? 0, location.Course ?? -1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in SendNowAsync: {ex.Message}");
            }
        }

        private async Task SendLocationAsync(Location location, double speed, double course)
        {
            try
            {
                var gpsData = new GpsDataDto
                {
                    IdVehicleRoute = _idVehicleRoute,
                    DateTime = DateTime.UtcNow,
                    Speed = speed,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Direction = GetCardinalDirection(course)
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
            return await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
        }

        private string GetCardinalDirection(double? bearing)
        {
            if (!bearing.HasValue || bearing < 0) return "N/A";
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(bearing.Value % 360 / 45)];
        }
    }
}