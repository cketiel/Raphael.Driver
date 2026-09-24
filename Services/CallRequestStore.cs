using CommunityToolkit.Mvvm.ComponentModel;
using Raphael.Driver.DTOs;
using System.Diagnostics;

namespace Raphael.Driver.Services
{
    public enum CallSignalResult
    {
        /// <summary>The office has it.</summary>
        Sent,

        /// <summary>Pressed again too soon: nothing changed, and the state says when it will count.</summary>
        TooSoon,

        /// <summary>The server could not be reached, or refused. Offer the office's phone.</summary>
        Failed,

        /// <summary>A press is already on its way, or there is no request to act on.</summary>
        Ignored
    }

    /// <summary>
    /// The driver's request to be called back, one copy for every screen that shows the button.
    /// </summary>
    /// <remarks>
    /// A singleton like <see cref="NotificationStore"/>, and for the same reason: the button on the
    /// schedule, on a stop and on the dashboard must never disagree.
    ///
    /// <para>
    /// It asks the server every thirty seconds only while a request is open and the app is in the
    /// foreground. The instant update comes from the push the office's "Attend" sends; the poll is
    /// what keeps it right when that push does not arrive.
    /// </para>
    /// </remarks>
    public partial class CallRequestStore : ObservableObject
    {
        /// <summary>The notification the office's "Attend" sends. Mirrors BusinessEventCodes on the server.</summary>
        public const string ClaimedEventCode = "DRIVER_CALL_REQUEST_CLAIMED";

        /// <summary>
        /// The office's number as last read from the server, kept for the moment it cannot be read:
        /// a phone with voice but no data can still dial.
        /// </summary>
        private const string OfficePhoneKey = "CallRequest.OfficePhone";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        private readonly ICallRequestService _api;

        private readonly IProviderService _providers;

        private IDispatcherTimer? _poll;

        private TimeSpan _clockSkew;

        private bool _isSending;

        /// <summary>Moves on every press, so a look that left before it cannot land after it.</summary>
        private int _pressCount;

        private bool _isInForeground = true;

        /// <summary>
        /// Between sign in and sign out. A token left in Preferences by an app that was killed is
        /// not a session: the login page must not start asking the server about the previous
        /// driver's request.
        /// </summary>
        private bool _isActive;

        [ObservableProperty]
        private DriverCallRequestDto? _state;

        [ObservableProperty]
        private bool _isBusy;

        public CallRequestStore(ICallRequestService api, IProviderService providers)
        {
            _api = api;
            _providers = providers;
        }

        public bool HasOpenRequest => State?.HasOpenRequest == true;

        public bool IsInProgress => HasOpenRequest && State!.IsInProgress;

        public bool MissedCall => HasOpenRequest && State!.MissedCallPending;

        /// <summary>The server's clock, as far as this phone can tell.</summary>
        public DateTime ServerNowUtc => DateTime.UtcNow + _clockSkew;

        public TimeSpan? WaitingFor =>
            HasOpenRequest && State!.RequestedAtUtc is DateTime at ? ServerNowUtc - at : null;

        /// <summary>How long until another press counts. Null when it counts now.</summary>
        public TimeSpan? NextSignalIn =>
            State?.NextSignalAllowedAtUtc is DateTime at && at > ServerNowUtc ? at - ServerNowUtc : null;

        private bool HasSession =>
            _isActive && !string.IsNullOrWhiteSpace(Preferences.Get("AuthToken", string.Empty));

        /// <summary>Called right after sign in.</summary>
        public async Task StartAsync()
        {
            _isActive = true;
            _isInForeground = true;

            await RefreshAsync();

            // Read now, while there is a connection, so the fallback has a number to dial later.
            await GetOfficePhoneAsync();
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (!HasSession)
                return;

            var pressesBefore = _pressCount;

            var fresh = await _api.GetCurrentAsync(cancellationToken);

            // A failed look keeps what is on screen: the next poll or press corrects it.
            if (fresh is null)
                return;

            // A press answered while this look was on its way already brought a newer state.
            // Applying this one would put the reminder count back where it was for thirty seconds.
            if (_isSending || pressesBefore != _pressCount)
                return;

            Apply(fresh);
        }

        public Task<CallSignalResult> RequestAsync(int? vehicleRouteId, int? scheduleId) =>
            SendAsync(async () =>
            {
                var location = await LastKnownLocationAsync();

                var answer = await _api.RequestAsync(new CreateCallRequestDto
                {
                    VehicleRouteId = vehicleRouteId,
                    ScheduleId = scheduleId,
                    Latitude = location?.Latitude,
                    Longitude = location?.Longitude
                });

                // A fresh fix moves the vehicle on the office's map now instead of at the next
                // tick. Not awaited: the driver must not wait for the GPS to hear the request
                // went through.
                if (answer is { SignalAccepted: true })
                    _ = SendFreshPositionAsync();

                return answer;
            });

        public Task<CallSignalResult> MarkAvailableAsync() =>
            State?.Id is int id
                ? SendAsync(() => _api.MarkAvailableAsync(id))
                : Task.FromResult(CallSignalResult.Ignored);

        public Task<CallSignalResult> CancelAsync() =>
            State?.Id is int id
                ? SendAsync(() => _api.CancelAsync(id))
                : Task.FromResult(CallSignalResult.Ignored);

        /// <summary>
        /// The office's number: fresh when the server answers, the last one it gave when it does not.
        /// </summary>
        /// <param name="preferSaved">
        /// Right after a request failed. The network just proved it is not there, and a second
        /// attempt would keep the driver waiting on a timeout before they can dial.
        /// </param>
        public async Task<string?> GetOfficePhoneAsync(bool preferSaved = false)
        {
            var saved = Preferences.Get(OfficePhoneKey, string.Empty);

            if (preferSaved && !string.IsNullOrWhiteSpace(saved))
                return saved;

            try
            {
                var provider = await _providers.GetContactProviderAsync();

                if (!string.IsNullOrWhiteSpace(provider?.Phone))
                {
                    Preferences.Set(OfficePhoneKey, provider.Phone);
                    return provider.Phone;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestStore: could not read the office phone. {ex.Message}");
            }

            return string.IsNullOrWhiteSpace(saved) ? null : saved;
        }

        /// <summary>The app went to the background: nobody is looking at the button.</summary>
        public void Pause()
        {
            _isInForeground = false;
            StopPolling();
        }

        /// <summary>Back in front: whatever happened meanwhile is only on the server.</summary>
        public async Task ResumeAsync()
        {
            _isInForeground = true;
            await RefreshAsync();
        }

        /// <summary>Called on sign out. The next driver on this phone starts with nothing.</summary>
        /// <remarks>The office's number stays: it is the company's, not the driver's.</remarks>
        public void Clear()
        {
            _isActive = false;

            OnMainThread(() =>
            {
                StopPolling();
                State = null;
                _clockSkew = TimeSpan.Zero;
            });
        }

        private async Task<CallSignalResult> SendAsync(Func<Task<DriverCallRequestDto?>> send)
        {
            // One press at a time. A second tap while the first is on its way would otherwise
            // count as a reminder the driver never meant to send.
            if (_isSending)
                return CallSignalResult.Ignored;

            // A press is proof of a session: the button only exists on pages behind the sign in.
            // Without this, a press in the seconds before sign in has finished starting the store
            // would reach the office and leave the button saying nothing was sent.
            _isActive = true;

            _isSending = true;
            _pressCount++;
            IsBusy = true;

            try
            {
                var answer = await send();

                if (answer is null)
                    return CallSignalResult.Failed;

                Apply(answer);

                return answer.SignalAccepted ? CallSignalResult.Sent : CallSignalResult.TooSoon;
            }
            finally
            {
                _isSending = false;
                IsBusy = false;
            }
        }

        private void Apply(DriverCallRequestDto fresh)
        {
            OnMainThread(() =>
            {
                // An answer that lands after sign out belongs to the driver who left.
                if (!_isActive)
                    return;

                if (fresh.ServerTimeUtc != default)
                    _clockSkew = fresh.ServerTimeUtc - DateTime.UtcNow;

                State = fresh;

                if (fresh.HasOpenRequest && _isInForeground)
                    StartPolling();
                else
                    StopPolling();
            });
        }

        partial void OnStateChanged(DriverCallRequestDto? value) => RaiseDerived();

        /// <summary>
        /// Everything computed from the state or from the clock. Raised on every poll too, so the
        /// minutes on the button keep moving even when the request itself has not changed.
        /// </summary>
        private void RaiseDerived()
        {
            OnPropertyChanged(nameof(HasOpenRequest));
            OnPropertyChanged(nameof(IsInProgress));
            OnPropertyChanged(nameof(MissedCall));
            OnPropertyChanged(nameof(WaitingFor));
            OnPropertyChanged(nameof(NextSignalIn));
        }

        private void StartPolling()
        {
            if (_poll is not null)
                return;

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null)
                return;

            _poll = dispatcher.CreateTimer();
            _poll.Interval = PollInterval;
            _poll.Tick += OnPollTick;
            _poll.Start();
        }

        private void StopPolling()
        {
            if (_poll is null)
                return;

            _poll.Stop();
            _poll.Tick -= OnPollTick;
            _poll = null;
        }

        private async void OnPollTick(object? sender, EventArgs e)
        {
            try
            {
                await RefreshAsync();

                RaiseDerived();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestStore: poll failed. {ex.Message}");
            }
        }

        /// <summary>
        /// Resolved here rather than injected. The first resolution of the GPS service decides
        /// which instance the whole app shares (MauiProgram), and this store must not move that
        /// moment earlier than it is today.
        /// </summary>
        private static async Task SendFreshPositionAsync()
        {
            try
            {
                var gps = ServiceHelper.GetService<IGpsService>();

                if (gps is not null)
                    await gps.SendNowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestStore: could not send a fresh position. {ex.Message}");
            }
        }

        /// <summary>
        /// Where the phone last knew it was. Instant, unlike a fresh fix, which can take ten seconds
        /// the driver would spend staring at a spinner.
        /// </summary>
        private static async Task<Location?> LastKnownLocationAsync()
        {
            try
            {
                return await Geolocation.Default.GetLastKnownLocationAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestStore: no last known location. {ex.Message}");
                return null;
            }
        }

        private static void OnMainThread(Action action)
        {
            if (MainThread.IsMainThread)
                action();
            else
                MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
