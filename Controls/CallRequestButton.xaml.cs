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
