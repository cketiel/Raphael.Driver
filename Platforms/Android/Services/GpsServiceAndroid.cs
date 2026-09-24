using Android.App;
using Android.Content;
using Android.OS;
using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;
using Debug = System.Diagnostics.Debug;

namespace Raphael.Driver.Services
{
    [Service(Name = "com.Raphael.Driver.Services.GpsServiceAndroid",
         ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
    public class GpsServiceAndroid : Service, IGpsService
    {
        private System.Timers.Timer _timer;
        private readonly HttpClient _httpClient;
        private int _idVehicleRoute;
        private GpsServiceBinder _binder;

        private const int SERVICE_RUNNING_NOTIFICATION_ID = 10000;
        private const string NOTIFICATION_CHANNEL_ID = "com.Raphael.Driver.gps";
        private const string NOTIFICATION_CHANNEL_NAME = "Gps Service";

        public bool IsTracking => _timer?.Enabled ?? false;

        public event Action<bool> IsTrackingChanged;

        public GpsServiceAndroid()
        {
            // On Android, we resolve the client from the MAUI container
            var factory = IPlatformApplication.Current.Services.GetRequiredService<IHttpClientFactory>();
            _httpClient = factory.CreateClient("GpsClient");

            //_httpClient = new HttpClient();
            //_httpClient.BaseAddress = new Uri("https://krasnovbw-001-site1.rtempurl.com/");
        }

        public override IBinder OnBind(Intent intent)
        {
            _binder = new GpsServiceBinder(this);
            return _binder;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            // The service now starts "empty" and waits for orders.
            if (intent != null && intent.HasExtra("vehicle_route_id"))
            {
                int vehicleRouteId = intent.GetIntExtra("vehicle_route_id", 0);
                if (vehicleRouteId > 0 && !IsTracking)
                {
                    StartTracking(vehicleRouteId);
                }
            }
            return StartCommandResult.Sticky;
        }

        public void StartTracking(int idVehicleRoute)
        {
            if (IsTracking) return;

            _idVehicleRoute = idVehicleRoute;
            PromoteToForegroundService();

            _timer = new System.Timers.Timer(30000); // 30 segundos
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

            StopForeground(true);
            StopSelf();

            IsTrackingChanged?.Invoke(false);
        }

        private void PromoteToForegroundService()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME, NotificationImportance.Default);
                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }

            var notification = new Notification.Builder(this, NOTIFICATION_CHANNEL_ID)
                .SetContentTitle("Raphael Driver")
                .SetContentText("Route tracking is active..")
                .SetSmallIcon(global::Raphael.Driver.Resource.Drawable.dotnet_bot)
                .SetOngoing(true)
                .Build();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notification, Android.Content.PM.ForegroundService.TypeLocation);
            }
            else
            {
                StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notification);
            }
        }

        public Task SendNowAsync() => IsTracking ? SendLocationAsync() : Task.CompletedTask;

        private async Task SendLocationAsync()
        {
            try
            {
                var location = await GetCurrentLocationAsync();
                if (location == null) return;

                string fullAddress = await GetReadableAddressAsync(location.Latitude, location.Longitude);

                var gpsData = new GpsDataDto
                {
                    IdVehicleRoute = _idVehicleRoute,
                    DateTime = DateTime.UtcNow,
                    Speed = location.Speed ?? 0,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Direction = GetCardinalDirection(location.Course),
                    Address = fullAddress
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to get location: {ex.Message}");
                return null;
            }
        }

        
        private string GetCardinalDirection(double? bearing)
        {
            if (!bearing.HasValue) return "N/A";
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(bearing.Value % 360 / 45)];
        }

        private async Task<string> GetReadableAddressAsync(double latitude, double longitude)
        {
            try
            {
                // Gets the possible directions for those coordinates
                var placemarks = await Geocoding.Default.GetPlacemarksAsync(latitude, longitude);
                var place = placemarks?.FirstOrDefault();

                if (place != null)
                {
                    // Expected format in the report: "6010 Pointe West Blvd, Bradenton, FL 34209, United States"
                    // Property mapping:
                    // SubThoroughfare = Número (6010)
                    // Thoroughfare = Calle (Pointe West Blvd)
                    // Locality = Ciudad (Bradenton)
                    // AdminArea = Estado (FL)
                    // PostalCode = CP (34209)
                    // CountryName = País (United States)

                    string street = $"{place.SubThoroughfare} {place.Thoroughfare}".Trim();

                    // If there is no street detected, sometimes FeatureName has the name of the place
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
    }

    // Binder class for communication
    public class GpsServiceBinder : Binder
    {
        public GpsServiceAndroid Service { get; }
        public GpsServiceBinder(GpsServiceAndroid service) => Service = service;
    }
}