using Android.Content;
using Android.OS;

namespace Raphael.Driver.Services
{
    /// <summary>
    /// What the app holds as its GPS service. Tracking always runs inside the real Android
    /// service, started as a foreground service: the app used to start it on a plain object that
    /// Android did not know about, so the location notice never showed and Android could stop the
    /// GPS as soon as the app went to the background.
    /// </summary>
    public sealed class GpsServiceProxy : IGpsService
    {
        private static IGpsService? Running => MainActivity.GpsService;

        public bool IsTracking => Running?.IsTracking ?? false;

        public event Action<bool> IsTrackingChanged
        {
            add => GpsServiceAndroid.TrackingChanged += value;
            remove => GpsServiceAndroid.TrackingChanged -= value;
        }

        public void StartTracking(int vehicleRouteId)
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(GpsServiceAndroid));
            intent.PutExtra(GpsServiceAndroid.ExtraVehicleRouteId, vehicleRouteId);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }

        public void StopTracking()
        {
            if (Running is { } service)
            {
                service.StopTracking();
                return;
            }
            try
            {
                var context = Android.App.Application.Context;
                var intent = new Intent(context, typeof(GpsServiceAndroid));
                intent.PutExtra(GpsServiceAndroid.ExtraStop, true);
                context.StartService(intent);
            }
            catch (Java.Lang.IllegalStateException)
            {
                // Not running and not allowed to start now: there is nothing to stop.
            }
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            if (Running is { } service) return await service.GetCurrentLocationAsync();
            try
            {
                return await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task SendNowAsync() => Running?.SendNowAsync() ?? Task.CompletedTask;
    }
}
