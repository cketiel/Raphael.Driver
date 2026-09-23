using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Services;
using System.ComponentModel;
using System.Diagnostics;

namespace Raphael.Driver.ViewModels
{
    /// <summary>
    /// What the "Call me" button shows, and what a tap on it does.
    /// </summary>
    /// <remarks>
    /// One per button: every page that shows it gets its own, all reading the same
    /// <see cref="CallRequestStore"/>. The control attaches it when it appears and detaches it when
    /// it goes, so the singleton store never holds on to a page that is gone.
    ///
    /// <para>
    /// Every sheet is one tap deep and every answer is a toast rather than a dialog: this is used
    /// with the van stopped at the side of the road, and each extra "OK" is one more look away
    /// from it.
    /// </para>
    /// </remarks>
    public partial class CallRequestButtonViewModel : ObservableObject
    {
        // FontAwesome 5 solid, the font the rest of the app uses.
        private const string PhoneGlyph = "";
        private const string HourglassGlyph = "";
        private const string HeadsetGlyph = "";
        private const string MissedGlyph = "";

        private const string RemindOption = "Remind dispatch";
        private const string AvailableOption = "I can talk now";
        private const string CancelOption = "I no longer need a call";
        private const string CloseOption = "Close";
        private const string RetryOption = "Try again";
        private const string CallOfficeOption = "Call the office";

        private const string Emergencies = "Emergencies: call 911.";

        private static readonly Color IdleColor = Color.FromArgb("#1565C0");
        private static readonly Color WaitingColor = Color.FromArgb("#E65100");
        private static readonly Color OnItColor = Color.FromArgb("#2E7D32");
        private static readonly Color MissedColor = Color.FromArgb("#C62828");

        private readonly CallRequestStore _store;

        private readonly IPhoneDialer _dialer;

        [ObservableProperty]
        private string _glyph = PhoneGlyph;

        [ObservableProperty]
        private string _text = "Call me";

        [ObservableProperty]
        private Color _surfaceColor = IdleColor;

        [ObservableProperty]
        private string _description = "Request a call from dispatch";

        [ObservableProperty]
        private bool _isBusy;

        public CallRequestButtonViewModel(CallRequestStore store, IPhoneDialer dialer)
        {
            _store = store;
            _dialer = dialer;
        }

        /// <summary>The route on screen, when the page knows it. Zero lets the server work it out.</summary>
        public int VehicleRouteId { get; set; }

        /// <summary>The stop on screen, when there is one. Tells the office what the driver was looking at.</summary>
        public int ScheduleId { get; set; }

        public void Attach()
        {
            _store.PropertyChanged += OnStoreChanged;
            Render();
        }

        public void Detach()
        {
            _store.PropertyChanged -= OnStoreChanged;
        }

        /// <summary>
        /// Explains the button the first time a driver sees it. Once per driver, not per phone:
        /// phones change hands between shifts.
        /// </summary>
        public async Task ShowIntroOnceAsync()
        {
            var key = $"CallRequest.IntroShown.{Preferences.Get("UserId", string.Empty)}";

            if (Preferences.Get(key, false))
                return;

            Preferences.Set(key, true);

            await Shell.Current.DisplayAlert(
                "New: Call me",
                "Need to talk to dispatch? Tap Call me, at the bottom right, instead of phoning. " +
                "Dispatch sees your request at once and calls you back with your route in front of them.\n\n" +
                "The button shows how it is going: orange while you wait, green when dispatch has it.\n\n" +
                Emergencies,
                "Got it");
        }

        [RelayCommand]
        private async Task Tap()
        {
            if (_store.HasOpenRequest)
            {
                // What the button shows may be up to thirty seconds old: the office may have closed
                // the request meanwhile, and "remind" on a closed request would open a new one.
                // A short look, though. On a weak signal the driver gets the sheet from what is
                // already known rather than a spinner.
                try
                {
                    IsBusy = true;

                    using var look = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await _store.RefreshAsync(look.Token);
                }
                finally
                {
                    IsBusy = _store.IsBusy;
                }
            }

            try
            {
                if (!_store.HasOpenRequest)
                    await RequestAsync();
                else if (_store.MissedCall)
                    await MissedCallSheetAsync();
                else
                    await OpenRequestSheetAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestButtonViewModel: {ex.Message}");
            }
        }

        private async Task RequestAsync()
        {
            // With nothing open, a wait can only come from the driver's own cancel moments ago.
            // Said before the confirmation, not after it: asking to confirm something that is going
            // to be refused wastes the driver's tap.
            if (_store.NextSignalIn is TimeSpan wait)
            {
                await ToastAsync($"You cancelled a moment ago. You can request a call again in {Countdown(wait)}.");
                return;
            }

            var state = _store.State;
            var phone = state?.CallbackPhone;

            // No state means the server has not answered yet in this session, not that there is no
            // number, so only an answer with an empty number earns the warning.
            string message;

            if (state is null)
                message = $"Dispatch will call you back.\n\n{Emergencies}";
            else if (string.IsNullOrWhiteSpace(phone))
                message = $"Dispatch has no phone number on file for you and may not be able to call you back.\n\n{Emergencies}";
            else
                message = $"Dispatch will call you at {phone}.\n\n{Emergencies}";

            var confirmed = await Shell.Current.DisplayAlert("Request a call", message, "Request call", "Cancel");

            if (!confirmed)
                return;

            await SendRequestAsync();
        }

        private async Task SendRequestAsync()
        {
            var result = await _store.RequestAsync(
                VehicleRouteId > 0 ? VehicleRouteId : null,
                ScheduleId > 0 ? ScheduleId : null);

            // Refused with nothing open: the driver cancelled their own request moments ago, and a
            // new one waits out the same gap as a reminder.
            if (result == CallSignalResult.TooSoon && !_store.HasOpenRequest)
            {
                var wait = _store.NextSignalIn;

                await ToastAsync(wait is TimeSpan left
                    ? $"You cancelled a moment ago. You can request a call again in {Countdown(left)}."
                    : "You cancelled a moment ago. Try again in a minute.");
                return;
            }

            await ReportAsync(result, "Request sent. Dispatch will call you.", SendRequestAsync);
        }

        private async Task OpenRequestSheetAsync()
        {
            var title = _store.IsInProgress
                ? $"{ClaimedBy()} has your request and will call you."
                : $"Waiting for dispatch · {WaitingText()}";

            var choice = await Shell.Current.DisplayActionSheet(title, CloseOption, CancelOption, RemindOption);

            if (choice == RemindOption)
                await RemindAsync();
            else if (choice == CancelOption)
                await CancelAsync();
        }

        private async Task MissedCallSheetAsync()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                $"{ClaimedBy()} tried to call you.", CloseOption, CancelOption, AvailableOption);

            if (choice == AvailableOption)
                await MarkAvailableAsync();
            else if (choice == CancelOption)
                await CancelAsync();
        }

        private async Task RemindAsync()
        {
            // Nothing to send yet: the server would only answer "too soon". Saying so here saves
            // the driver a round trip on a weak signal.
            if (_store.NextSignalIn is TimeSpan wait)
            {
                await ToastAsync($"Dispatch already has it. You can remind them again in {Countdown(wait)}.");
                return;
            }

            var result = await _store.RequestAsync(
                VehicleRouteId > 0 ? VehicleRouteId : null,
                ScheduleId > 0 ? ScheduleId : null);

            await ReportAsync(result, "Reminder sent.", RemindAsync);
        }

        private async Task MarkAvailableAsync()
        {
            var result = await _store.MarkAvailableAsync();

            // Too soon here means the missed call was already answered, from this phone or by the office.
            if (result == CallSignalResult.TooSoon)
            {
                await ToastAsync("Dispatch already knows.");
                return;
            }

            await ReportAsync(result, "Dispatch knows you can talk now.", MarkAvailableAsync);
        }

        private async Task CancelAsync()
        {
            var result = await _store.CancelAsync();

            // Too soon here means there was nothing open to cancel any more.
            if (result == CallSignalResult.TooSoon)
            {
                await ToastAsync("Nothing to cancel: dispatch already closed it.");
                return;
            }

            await ReportAsync(result, "Request cancelled.", CancelAsync);
        }

        private async Task ReportAsync(CallSignalResult result, string sent, Func<Task> retry)
        {
            switch (result)
            {
                case CallSignalResult.Sent:
                    await ToastAsync(sent);
                    break;

                case CallSignalResult.TooSoon:
                    var wait = _store.NextSignalIn;
                    await ToastAsync(wait is TimeSpan left
                        ? $"Dispatch already has it. You can send again in {Countdown(left)}."
                        : "Dispatch already has it.");
                    break;

                case CallSignalResult.Failed:
                    await FallbackAsync(retry);
                    break;
            }
        }

        /// <summary>
        /// The safety valve. A request that cannot get through must never leave the driver without
        /// a way to reach the office, so the phone call is always one tap away from here.
        /// </summary>
        private async Task FallbackAsync(Func<Task> retry)
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "The request did not reach dispatch. Check your connection.",
                CloseOption,
                null,
                RetryOption,
                CallOfficeOption);

            if (choice == RetryOption)
                await retry();
            else if (choice == CallOfficeOption)
                await CallOfficeAsync();
        }

        private async Task CallOfficeAsync()
        {
            var phone = await _store.GetOfficePhoneAsync(preferSaved: true);

            if (string.IsNullOrWhiteSpace(phone))
            {
                await Shell.Current.DisplayAlert("Not available", "The office phone number is not available.", "OK");
                return;
            }

            try
            {
                if (_dialer.IsSupported)
                    _dialer.Open(phone);
                else
                    await Shell.Current.DisplayAlert("Not supported", "This device cannot make calls.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestButtonViewModel: could not dial. {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "The call could not be started.", "OK");
            }
        }

        private void OnStoreChanged(object? sender, PropertyChangedEventArgs e) => Render();

        private void Render()
        {
            void Apply()
            {
                IsBusy = _store.IsBusy;

                if (!_store.HasOpenRequest)
                {
                    Glyph = PhoneGlyph;
                    Text = "Call me";
                    SurfaceColor = IdleColor;
                    Description = "Request a call from dispatch";
                    return;
                }

                if (_store.MissedCall)
                {
                    Glyph = MissedGlyph;
                    Text = "Missed call";
                    SurfaceColor = MissedColor;
                    Description = "Dispatch tried to call you. Tap to tell them you can talk now.";
                    return;
                }

                if (_store.IsInProgress)
                {
                    Glyph = HeadsetGlyph;
                    Text = "On it";
                    SurfaceColor = OnItColor;
                    Description = $"{ClaimedBy()} has your request and will call you.";
                    return;
                }

                var presses = (_store.State?.ReminderCount ?? 0) + 1;

                Glyph = HourglassGlyph;
                Text = presses > 1 ? $"{WaitingText()} ×{presses}" : WaitingText();
                SurfaceColor = WaitingColor;
                Description = $"Waiting for dispatch, {WaitingText()}. Asked {presses} times.";
            }

            if (MainThread.IsMainThread)
                Apply();
            else
                MainThread.BeginInvokeOnMainThread(Apply);
        }

        /// <summary>First name only, as the server sends it: enough to know who is on the phone.</summary>
        private string ClaimedBy()
        {
            var name = _store.State?.ClaimedByFirstName;

            return string.IsNullOrWhiteSpace(name) ? "Dispatch" : $"{name} from dispatch";
        }

        private string WaitingText()
        {
            var minutes = (int)Math.Floor(Math.Max(0, _store.WaitingFor?.TotalMinutes ?? 0));

            return minutes < 1 ? "Sent" : $"{minutes} min";
        }

        private static string Countdown(TimeSpan wait) =>
            wait.TotalSeconds < 60
                ? $"{Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))} s"
                : $"{(int)wait.TotalMinutes}:{wait.Seconds:00} min";

        private static async Task ToastAsync(string text)
        {
            try
            {
                await Toast.Make(text, ToastDuration.Long).Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestButtonViewModel: could not show a toast. {ex.Message}");
            }
        }
    }
}
