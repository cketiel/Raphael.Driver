using Raphael.Driver.Services;
using System.ComponentModel;
using System.Diagnostics;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// Centred page icon and title, with an optional notification bell and unread counter.
    /// </summary>
    /// <remarks>
    /// Used as <c>Shell.TitleView</c>. Android left-aligns the Shell title and gives no way to
    /// centre it, so the title area is replaced wholesale.
    ///
    /// <para>
    /// The bell reads the count straight from the singleton <see cref="NotificationStore"/>
    /// rather than keeping one of its own, so the badge and the notifications page cannot end
    /// up disagreeing.
    /// </para>
    /// </remarks>
    public partial class PageTitleBar : ContentView
    {
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(
                nameof(Title),
                typeof(string),
                typeof(PageTitleBar),
                string.Empty);

        /// <summary>
        /// FontAwesome glyph shown to the left of the title. Empty means no icon.
        /// </summary>
        /// <remarks>
        /// The same glyph the page carries in the side menu, so that a screen names itself the
        /// same way wherever the driver meets it.
        /// </remarks>
        public static readonly BindableProperty IconProperty =
            BindableProperty.Create(
                nameof(Icon),
                typeof(string),
                typeof(PageTitleBar),
                string.Empty,
                propertyChanged: OnIconChanged);

        public static readonly BindableProperty AccessoryProperty =
            BindableProperty.Create(nameof(Accessory), typeof(View), typeof(PageTitleBar), null,
                propertyChanged: (b, _, n) => ((PageTitleBar)b).AccessoryHost.Content = (View?)n);

        /// <summary>RE-064: a view in the bell's place, on the right of the bar (Call me).</summary>
        public View? Accessory
        {
            get => (View?)GetValue(AccessoryProperty);
            set => SetValue(AccessoryProperty, value);
        }

        public static readonly BindableProperty ShowBellProperty =
            BindableProperty.Create(
                nameof(ShowBell),
                typeof(bool),
                typeof(PageTitleBar),
                true,
                propertyChanged: OnShowBellChanged);

        /// <summary>Text shown centred in the navigation bar.</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>Icon of the page, in the FontAwesomeSolid font.</summary>
        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>
        /// Whether the bell is shown. Off for pages the driver reaches from somewhere else:
        /// a bell on a detail page competes with the back arrow for the same glance.
        /// </summary>
        public bool ShowBell
        {
            get => (bool)GetValue(ShowBellProperty);
            set => SetValue(ShowBellProperty, value);
        }

        private NotificationStore? _store;

        public PageTitleBar()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // The offset to correct is only known once the toolbar has laid this out, and it
            // changes with the navigation icon: a root page shows a hamburger, a pushed page a
            // back arrow, and they are not the same width.
            SizeChanged += (_, _) => ScheduleCentring();
        }

        /// <summary>
        /// Asks for the title to be re-centred once the layout pass that triggered this is over.
        /// </summary>
        /// <remarks>
        /// Measuring inside the layout pass reads a control that has a width but not yet a
        /// position, and a correction computed from that would push the title further off than
        /// it started.
        /// </remarks>
#if ANDROID
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler?.PlatformView is Android.Views.View native)
                native.LayoutChange += (_, _) => CentreTitleOnScreen();
        }
#endif

        private void ScheduleCentring()
        {
            Dispatcher.Dispatch(CentreTitleOnScreen);
        }

        /// <summary>
        /// Moves the title so that it sits on the middle of the window, not the middle of this
        /// control.
        /// </summary>
        /// <remarks>
        /// Android lays a Shell TitleView out after the navigation icon, so this control starts
        /// some fifty units in from the left edge and its centre is that far to the right of
        /// the window centre. A title centred inside it therefore reads as pushed to the right,
        /// which is exactly what came back from testing.
        ///
        /// <para>
        /// Measured rather than guessed. Hard-coding the width of a hamburger is a number that
        /// is wrong on the next device, on a back arrow, and at a different font scale; asking
        /// the platform where the control actually landed is right on all of them.
        /// </para>
        ///
        /// <para>
        /// Only the icon and the title move. The bell stays pinned to the right edge, where it
        /// belongs.
        /// </para>
        /// </remarks>
        private void CentreTitleOnScreen()
        {
#if ANDROID
            try
            {
                if (Handler?.PlatformView is not Android.Views.View native)
                    return;

                // Not placed yet. Whatever fires next — the next size change, or the load —
                // asks again, and there is nothing to correct until then anyway.
                if (!native.IsLaidOut || native.Width <= 0)
                    return;

                // The window, reached through the view tree rather than through DeviceDisplay:
                // in split screen the app owns half the screen, and the title has to be centred
                // on what the driver is looking at.
                Android.Views.View root = native;

                while (root.Parent is Android.Views.View parent)
                    root = parent;

                if (root.Width <= 0)
                    return;

                var density = native.Context?.Resources?.DisplayMetrics?.Density ?? 0f;

                if (density <= 0f)
                    return;

                var barLocation = new int[2];
                var rootLocation = new int[2];

                native.GetLocationOnScreen(barLocation);
                root.GetLocationOnScreen(rootLocation);

                var barCentre = barLocation[0] + (native.Width / 2.0);
                var windowCentre = rootLocation[0] + (root.Width / 2.0);

                var shift = (windowCentre - barCentre) / density;

                TitleContent.TranslationX = Math.Abs(shift) < 0.5 ? 0 : shift;
            }
            catch (Exception ex)
            {
                // A title a few units off centre is a blemish. Taking the navigation bar down
                // over it would not be.
                Debug.WriteLine($"PageTitleBar: could not centre the title. {ex.Message}");
            }
#endif
        }

        private static void OnShowBellChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is PageTitleBar bar)
                bar.BellArea.IsVisible = (bool)newValue;
        }

        private static void OnIconChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is PageTitleBar bar)
                bar.IconLabel.IsVisible = !string.IsNullOrEmpty(newValue as string);
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            BellArea.IsVisible = ShowBell;
            IconLabel.IsVisible = !string.IsNullOrEmpty(Icon);

            ScheduleCentring();

            if (!ShowBell)
                return;

            _store = ServiceHelper.GetService<NotificationStore>();

            if (_store is null)
                return;

            _store.PropertyChanged += OnStoreChanged;

            UpdateBadge();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            // The store outlives the page. Without this the singleton keeps a reference to
            // every title bar that was ever shown.
            if (_store is not null)
                _store.PropertyChanged -= OnStoreChanged;

            _store = null;
        }

        private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NotificationStore.UnreadCount))
                UpdateBadge();
        }

        private void UpdateBadge()
        {
            var count = _store?.UnreadCount ?? 0;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                BadgeBorder.IsVisible = count > 0;

                // Past ninety-nine the exact number stops telling the driver anything they
                // did not already know.
                BadgeLabel.Text = count > 99 ? "99+" : count.ToString();
            });
        }

        private async void OnBellTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("//NotificationsPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PageTitleBar: could not navigate. {ex.Message}");
            }
        }
    }
}
