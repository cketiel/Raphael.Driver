using Raphael.Driver.Services;
using Raphael.Driver.ViewModels;
using System.Diagnostics;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// The floating "Call me" button. See the XAML for what it looks like and where it goes.
    /// </summary>
    /// <remarks>
    /// Only wiring lives here, as in <see cref="PageTitleBar"/>: what the button shows and what a
    /// tap does are in <see cref="CallRequestButtonViewModel"/>.
    /// </remarks>
    public partial class CallRequestButton : ContentView
    {
        /// <summary>The route on the page, when it has one. Zero lets the server work it out.</summary>
        public static readonly BindableProperty VehicleRouteIdProperty =
            BindableProperty.Create(
                nameof(VehicleRouteId),
                typeof(int),
                typeof(CallRequestButton),
                0,
                propertyChanged: (bindable, _, value) =>
                {
                    if (bindable is CallRequestButton button && button._viewModel is not null)
                        button._viewModel.VehicleRouteId = (int)value;
                });

        /// <summary>The stop on the page, when it shows one.</summary>
        public static readonly BindableProperty ScheduleIdProperty =
            BindableProperty.Create(
                nameof(ScheduleId),
                typeof(int),
                typeof(CallRequestButton),
                0,
                propertyChanged: (bindable, _, value) =>
                {
                    if (bindable is CallRequestButton button && button._viewModel is not null)
                        button._viewModel.ScheduleId = (int)value;
                });

        /// <summary>
        /// Whether this page explains the button the first time a driver sees it. Only the page the
        /// shift starts on, so the explanation arrives once and in the right place.
        /// </summary>
        public static readonly BindableProperty ShowIntroProperty =
            BindableProperty.Create(
                nameof(ShowIntro),
                typeof(bool),
                typeof(CallRequestButton),
                false);

        private readonly CallRequestButtonViewModel? _viewModel;

        private bool _isAttached;

        public CallRequestButton()
        {
            InitializeComponent();

            _viewModel = ServiceHelper.GetService<CallRequestButtonViewModel>();

            // Set before anything binds, so the surface never sees the page's view model through
            // inheritance.
            Surface.BindingContext = _viewModel;

            // Nothing to show without it. A missing button is better than a button that does nothing.
            IsVisible = _viewModel is not null;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public int VehicleRouteId
        {
            get => (int)GetValue(VehicleRouteIdProperty);
            set => SetValue(VehicleRouteIdProperty, value);
        }

        public int ScheduleId
        {
            get => (int)GetValue(ScheduleIdProperty);
            set => SetValue(ScheduleIdProperty, value);
        }

        public bool ShowIntro
        {
            get => (bool)GetValue(ShowIntroProperty);
            set => SetValue(ShowIntroProperty, value);
        }

        public static readonly BindableProperty CompactProperty =
            BindableProperty.Create(nameof(Compact), typeof(bool), typeof(CallRequestButton), false,
                propertyChanged: (b, _, n) => ((CallRequestButton)b).ApplyCompact((bool)n));

        /// <summary>
        /// RE-064: the button as a round icon in the title bar, where the bell used to be, so it no
        /// longer floats over the cards. The colour and the icon still say how the request is going
        /// (blue, orange, green, red); the words are read out to screen readers as before.
        /// </summary>
        public bool Compact
        {
            get => (bool)GetValue(CompactProperty);
            set => SetValue(CompactProperty, value);
        }

        private void ApplyCompact(bool compact)
        {
            Margin = compact ? new Thickness(0) : new Thickness(0, 0, 16, 20);
            HorizontalOptions = compact ? LayoutOptions.Center : LayoutOptions.End;
            VerticalOptions = compact ? LayoutOptions.Center : LayoutOptions.End;
            Surface.HeightRequest = compact ? 40 : 64;
            Surface.MinimumWidthRequest = compact ? 40 : 64;
            Surface.WidthRequest = compact ? 40 : -1;
            Surface.Padding = compact ? new Thickness(0) : new Thickness(20, 0);
            Surface.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = compact ? 20 : 32 };
            Surface.StrokeThickness = compact ? 2 : 0;
            Surface.Stroke = compact ? Brush.White : null;
            Surface.Shadow = compact ? null! : new Shadow { Brush = Colors.Black, Opacity = .35f, Radius = 8, Offset = new Point(0, 3) };
            TextLabel.IsVisible = !compact;
            Row.HorizontalOptions = compact ? LayoutOptions.Center : LayoutOptions.Fill;
        }

        /// <summary>The same as tapping this button; All options › Call me uses it (RE-064).</summary>
        public void Tap() => _viewModel?.TapCommand.Execute(null);

        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (_viewModel is null || _isAttached)
                return;

            _isAttached = true;

            _viewModel.VehicleRouteId = VehicleRouteId;
            _viewModel.ScheduleId = ScheduleId;
            _viewModel.Attach();

            if (!ShowIntro)
                return;

            try
            {
                await _viewModel.ShowIntroOnceAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallRequestButton: could not show the introduction. {ex.Message}");
            }
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            // The store outlives the page. Without this the singleton keeps a reference to every
            // button that was ever shown.
            if (_viewModel is null || !_isAttached)
                return;

            _isAttached = false;

            _viewModel.Detach();
        }
    }
}
