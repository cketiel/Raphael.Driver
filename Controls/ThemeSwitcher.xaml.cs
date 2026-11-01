using Raphael.Driver.Services;

namespace Raphael.Driver.Controls
{
    /// <summary>
    /// Four dots, one per V7 theme; tapping one applies it through <see cref="ThemeManager"/>.
    /// </summary>
    public partial class ThemeSwitcher : ContentView
    {
        public static readonly BindableProperty CapsuleColorProperty =
            BindableProperty.Create(nameof(CapsuleColor), typeof(Color), typeof(ThemeSwitcher), Colors.Transparent);

        public static readonly BindableProperty RingColorProperty =
            BindableProperty.Create(nameof(RingColor), typeof(Color), typeof(ThemeSwitcher), Colors.White);

        /// <summary>Background of the capsule, so it reads on whatever the switch sits on.</summary>
        public Color CapsuleColor
        {
            get => (Color)GetValue(CapsuleColorProperty);
            set => SetValue(CapsuleColorProperty, value);
        }

        /// <summary>Colour of the ring around the dot of the theme in use.</summary>
        public Color RingColor
        {
            get => (Color)GetValue(RingColorProperty);
            set => SetValue(RingColorProperty, value);
        }

        public ThemeSwitcher()
        {
            InitializeComponent();
            ShowSelected();

            // Subscribed only while on screen: a page that is gone must not keep this alive.
            Loaded += (_, _) => ThemeManager.ThemeChanged += OnThemeChanged;
            Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e) => ShowSelected();

        private void OnLightTapped(object? sender, TappedEventArgs e) => ThemeManager.Select(SkinTheme.Light);

        private void OnGrayTapped(object? sender, TappedEventArgs e) => ThemeManager.Select(SkinTheme.Gray);

        private void OnDarkTapped(object? sender, TappedEventArgs e) => ThemeManager.Select(SkinTheme.Dark);

        private void OnInfraredTapped(object? sender, TappedEventArgs e) => ThemeManager.Select(SkinTheme.Infrared);

        /// <summary>Rings the theme on screen; while following the phone that is Light or Dark.</summary>
        private void ShowSelected()
        {
            var effective = ThemeManager.Effective;
            LightRing.IsVisible = effective == SkinTheme.Light;
            GrayRing.IsVisible = effective == SkinTheme.Gray;
            DarkRing.IsVisible = effective == SkinTheme.Dark;
            InfraredRing.IsVisible = effective == SkinTheme.Infrared;
        }
    }
}
