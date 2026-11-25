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

        // Same cadence as Android (GpsServiceAndroid): one fix every 30 seconds. CoreLocation keeps
        // delivering updates so iOS keeps the app alive in the background; the timer decides when
        // the latest one is sent, so the server sees the same rhythm from both platforms.
        private const int SendIntervalMs = 30000;
        private System.Timers.Timer? _timer;
        private IosLocation? _lastLocation;

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
            // No distance filter: a driver waiting at a pickup must still report every 30 s,
            // exactly as on Android. Sending is throttled by the timer, not by CoreLocation.
            _locationManager.DistanceFilter = CLLocationDistance.FilterNone;
            _locationManager.ShowsBackgroundLocationIndicator = true;
            _locationManager.StartUpdatingLocation();

            _timer = new System.Timers.Timer(SendIntervalMs);
            _timer.Elapsed += async (s, e) => await SendLatestAsync();
            _timer.Start();
            _ = SendNowAsync();

            _isTracking = true;
            Debug.WriteLine("iOS GPS Tracking Started");
            IsTrackingChanged?.Invoke(true);
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _locationManager?.StopUpdatingLocation();
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            _lastLocation = null;
            _isTracking = false;
            Debug.WriteLine("iOS GPS Tracking Stopped");
            IsTrackingChanged?.Invoke(false);
        }

        // CoreLocation calls this with every new fix; it is only kept. The timer sends it.
        [Export("locationManager:didUpdateLocations:")]
        public void LocationsUpdated(CLLocationManager manager, IosLocation[] locations)
        {
            var last = locations.LastOrDefault();
            if (last != null)
                _lastLocation = last;
        }

        private async Task SendLatestAsync()
        {
            var last = _lastLocation;
            if (last == null)
            {
                await SendNowAsync();
                return;
            }

            var location = new Location(last.Coordinate.Latitude, last.Coordinate.Longitude);
            await SendLocationAsync(location, Math.Max(last.Speed, 0), last.Course);
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
                    Direction = GetCardinalDirection(course),
                    Address = await GetReadableAddressAsync(location.Latitude, location.Longitude)
                };

                // RE-062: a fix that cannot be sent is kept on the phone and goes with the next one
                // that can, so a stretch without coverage is not a stretch without miles.
                HttpResponseMessage? response = null;
                try { response = await _httpClient.PostAsJsonAsync("api/Gps", gpsData); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }

                var status = (int?)response?.StatusCode;
                if (response is null || status >= 500 || status is 401 or 408 or 429)
                {
                    Debug.WriteLine($"GPS fix kept on the phone: {response?.ReasonPhrase ?? "no connection"}");
                    Raphael.Driver.Services.Offline.GpsBuffer.Add(gpsData);
                }
                else if (response.IsSuccessStatusCode)
                {
                    _ = Raphael.Driver.Services.Offline.GpsBuffer.FlushAsync(_httpClient);
                }
                else
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

        // Same format as Android: "6010 Pointe West Blvd, Bradenton, FL 34209, United States".
        private static async Task<string> GetReadableAddressAsync(double latitude, double longitude)
        {
            try
            {
                var place = (await Geocoding.Default.GetPlacemarksAsync(latitude, longitude))?.FirstOrDefault();
                if (place != null)
                {
                    string street = $"{place.SubThoroughfare} {place.Thoroughfare}".Trim();
                    if (string.IsNullOrEmpty(street))
                        street = place.FeatureName;

                    return $"{street}, {place.Locality}, {place.AdminArea} {place.PostalCode}, {place.CountryName}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting address: {ex.Message}");
            }

            return "Address not available";
        }

        private string GetCardinalDirection(double? bearing)
        {
            if (!bearing.HasValue || bearing < 0) return "N/A";
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(bearing.Value % 360 / 45)];
        }
    }
}