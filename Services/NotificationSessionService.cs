using System.Diagnostics;

namespace Raphael.Driver.Services
{
    /// <summary>
    /// Turns notifications on when a driver signs in and off when they sign out.
    /// </summary>
    /// <remarks>
    /// Kept in one place so the two halves cannot drift apart. ⚠️ The sign out half matters as
    /// much as the sign in half: phones are handed over between shifts, and a device left
    /// registered keeps receiving the previous driver's notifications — trips that are not
    /// theirs, on somebody else's screen.
    /// </remarks>
    public class NotificationSessionService
    {
        private readonly INotificationApiService _api;
        private readonly INotificationHubService _hub;
        private readonly NotificationStore _store;
        private readonly HiddenNotificationStore _hidden;
        private readonly ConsumedSignalStore _consumedSignals;
        private readonly IPushTokenProvider _pushTokens;
        private readonly RouteSignalCoordinator _signals;
        private readonly CallRequestStore _callRequests;

        public NotificationSessionService(
            INotificationApiService api,
            INotificationHubService hub,
            NotificationStore store,
            HiddenNotificationStore hidden,
            ConsumedSignalStore consumedSignals,
            IPushTokenProvider pushTokens,
            RouteSignalCoordinator signals,
            CallRequestStore callRequests)
        {
            _signals = signals;
            _callRequests = callRequests;
            _api = api;
            _hub = hub;
            _store = store;
            _hidden = hidden;
            _consumedSignals = consumedSignals;
            _pushTokens = pushTokens;
        }

        /// <summary>
        /// Called right after a successful sign in, once the token is in Preferences.
        /// </summary>
        public async Task StartAsync()
        {
            _hidden.LoadForCurrentUser();

            // Both lists belong to the driver signing in, not to the phone: what the previous
            // driver dismissed, and the route changes their app already acted on.
            _consumedSignals.LoadForCurrentUser();

            // The inbox first: it is the part that works without Firebase, without a live
            // socket and without permission being granted.
            await _store.RefreshAsync();

            // Started here, before the hub and the permission dialog, so a request the driver left
            // open before the app closed is on the button from the start. Not awaited: it also
            // reads the office's phone number, and on a weak signal that must not hold back the
            // notifications that were already here. It swallows its own errors.
            _ = _callRequests.StartAsync();

            await _hub.StartAsync();

            // Route changes that happened while the app was closed. Harmless if stale: the
            // screens reload on the way in anyway.
            await _signals.SyncAsync();

            await RegisterDeviceAsync();
        }

        /// <summary>
        /// Called on sign out, <b>before</b> the session is wiped: both the API call and the
        /// hub need the token that is about to disappear.
        /// </summary>
        public async Task StopAsync()
        {
            // First, and without waiting on the network: with no signal the calls below can hang
            // well past the five seconds sign out gives them, and a clear that lands after the
            // next driver signed in would wipe their button instead. The request itself stays
            // open on the server, where the office still sees it and can still call.
            _callRequests.Clear();

            try
            {
                await _api.ClearPushTokenAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotificationSessionService: could not clear the push token. {ex.Message}");
            }

            Preferences.Remove(PushTokenProvider.TokenPreferenceKey);

            await _hub.StopAsync();

            await _signals.ClearAsync();

            _store.Clear();
        }

        /// <summary>
        /// Asks for permission, gets the FCM token and hands it to the API.
        /// </summary>
        public async Task RegisterDeviceAsync()
        {
            if (!_pushTokens.IsSupported)
                return;

            try
            {
                var granted = await _pushTokens.RequestPermissionAsync();

                if (!granted)
                {
                    // Refusing the permission only costs the driver the push. Everything else
                    // keeps working, so there is nothing to argue about here.
                    Debug.WriteLine("NotificationSessionService: notification permission not granted.");
                    return;
                }

                var token = await _pushTokens.GetTokenAsync();

                if (string.IsNullOrWhiteSpace(token))
                    return;

                await SendTokenAsync(token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotificationSessionService: device registration failed. {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a token with the API. Also the entry point for the token Firebase hands
        /// us out of the blue when it rotates one.
        /// </summary>
        public async Task SendTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            if (string.IsNullOrWhiteSpace(Preferences.Get("AuthToken", string.Empty)))
                return;

            var registered = await _api.RegisterPushTokenAsync(token);

            if (registered)
                Preferences.Set(PushTokenProvider.TokenPreferenceKey, token);
        }
    }
}
