using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using System.Diagnostics;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// Tells the driver the route changed, then resolves itself.
    /// </summary>
    /// <remarks>
    /// See the comment in the XAML for why it both blocks and closes on its own. In short:
    /// the driver must not be able to tap a list that is about to reorder, and must not be
    /// required to tap anything to get correct data.
    /// </remarks>
    public partial class RouteSignalPopup : Popup
    {
        /// <summary>
        /// Long enough to read two lines at the wheel, short enough that the schedule on
        /// screen is not wrong for meaningfully longer than it already was.
        /// </summary>
        public const int DefaultSeconds = 5;

        private readonly CancellationTokenSource _cancellation = new();

        private RouteSignalPopup(string title, string message, string actionLabel)
        {
            InitializeComponent();

            TitleLabel.Text = title;
            MessageLabel.Text = message;
            ActionButton.Text = actionLabel;
        }

        /// <summary>
        /// Shows it and returns when the countdown ends or the driver taps the button.
        /// </summary>
        /// <remarks>
        /// ⚠️ Marshalled onto the UI thread here, not left to the callers. What raises this is
        /// a signal arriving over SignalR, which runs on its own thread, and Android refuses to
        /// build a dialog from anywhere but the main one. The throw was caught below and only
        /// written to the debug log, so the countdown ran, the schedule reloaded, and the
        /// driver saw a list rearrange itself with nothing on screen to explain it.
        /// </remarks>
        public static Task ShowAsync(
            string title,
            string message,
            string actionLabel,
            int seconds = DefaultSeconds)
        {
            return MainThread.InvokeOnMainThreadAsync(
                () => ShowOnMainThreadAsync(title, message, actionLabel, seconds));
        }

        private static async Task ShowOnMainThreadAsync(
            string title,
            string message,
            string actionLabel,
            int seconds)
        {
            var page = Shell.Current?.CurrentPage;

            if (page is null)
                return;

            var popup = new RouteSignalPopup(title, message, actionLabel);

            var countdown = popup.RunCountdownAsync(seconds);

            try
            {
                await page.ShowPopupAsync(popup, new CommunityToolkit.Maui.PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false, Shape = null, Shadow = null });
            }
            catch (Exception ex)
            {
                // A popup that cannot be shown must not stop the refresh: the driver ends up
                // with correct data without an explanation, which beats stale data with one.
                Debug.WriteLine($"RouteSignalPopup: could not show. {ex.Message}");
            }

            await countdown;
        }

        private async Task RunCountdownAsync(int seconds)
        {
            try
            {
                for (var remaining = seconds; remaining > 0; remaining--)
                {
                    UpdateCountdown(remaining);

                    await Task.Delay(1000, _cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // The driver pressed the button. Nothing to do: the popup is already closing.
                return;
            }

            await CloseSafelyAsync();
        }

        private void UpdateCountdown(int remaining)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                CountdownLabel.Text = remaining == 1
                    ? "Continuing in 1 second"
                    : $"Continuing in {remaining} seconds");
        }

        private async void OnActionClicked(object? sender, EventArgs e)
        {
            _cancellation.Cancel();

            await CloseSafelyAsync();
        }

        private async Task CloseSafelyAsync()
        {
            try
            {
                await CloseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RouteSignalPopup: could not close. {ex.Message}");
            }
        }
    }
}
