using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using Raphael.Driver.Resources.Styles;

namespace Raphael.Driver.Services
{
    /// <summary>The four V7 themes, plus following the phone.</summary>
    public enum SkinTheme
    {
        /// <summary>Light or dark as the phone is set. What the app did before themes existed.</summary>
        System,
        Light,
        Gray,
        Dark,
        Infrared
    }

    /// <summary>
    /// Which theme the app paints with, and the one place that changes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A theme is a whole <see cref="ResourceDictionary"/> (Resources/Styles/*Theme.xaml), all
    /// four declaring the same keys. Applying one swaps the dictionary merged after Colors.xaml;
    /// everything read with <c>DynamicResource</c> repaints on its own.
    /// </para>
    /// <para>
    /// The choice belongs to the driver and is stored on the phone. Until they pick one the app
    /// follows the phone's light/dark setting, so a driver who never touches the switch sees
    /// what they saw before.
    /// </para>
    /// <para>
    /// ⚠️ <see cref="Application.UserAppTheme"/> is set too, so native pieces the app does not
    /// draw — alerts, prompts, the keyboard — come out light or dark to match. Setting it raises
    /// <see cref="Application.RequestedThemeChanged"/>, which is why that event is only acted on
    /// while following the phone.
    /// </para>
    /// </remarks>
    public static class ThemeManager
    {
        private const string PreferenceKey = "SkinTheme";

        private static ResourceDictionary? _applied;

        public static SkinTheme Selected { get; private set; } = SkinTheme.System;

        /// <summary>The theme actually on screen: <see cref="SkinTheme.System"/> resolved to Light or Dark.</summary>
        public static SkinTheme Effective { get; private set; } = SkinTheme.Light;

        /// <summary>Raised after a theme has been applied, for the few colours that live in code.</summary>
        public static event EventHandler? ThemeChanged;

        /// <summary>Reads the stored choice and applies it. Call before the Shell is created.</summary>
        public static void Initialize(Application app)
        {
            Selected = ReadPreference();
            app.RequestedThemeChanged += (_, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] phone theme now {e.RequestedTheme}, following phone: {Selected == SkinTheme.System}");

                // The event's own value: PlatformAppTheme can still hold the old one at this point.
                if (Selected == SkinTheme.System)
                    MainThread.BeginInvokeOnMainThread(() => Apply(app, SkinTheme.System, persist: false, e.RequestedTheme));
            };

            Apply(app, Selected, persist: false);
        }

        /// <summary>
        /// The phone went light or dark (MainActivity reports it). Repaints only while following
        /// the phone, and only if the theme on screen is not already that one.
        /// </summary>
        public static void OnPhoneThemeChanged(AppTheme phoneTheme)
        {
            if (Selected != SkinTheme.System || Application.Current is not { } app)
                return;

            var wanted = phoneTheme == AppTheme.Dark ? SkinTheme.Dark : SkinTheme.Light;

            if (wanted != Effective)
                MainThread.BeginInvokeOnMainThread(() => Apply(app, SkinTheme.System, persist: false, phoneTheme));
        }

        /// <summary>Applies <paramref name="theme"/> and remembers it on this phone.</summary>
        public static void Select(SkinTheme theme)
        {
            if (Application.Current is { } app)
                Apply(app, theme, persist: true);
        }

        /// <summary>
        /// Paints the status bar a shade darker than the top bar (StatusBarColor): the faint seam
        /// between the two is the V7 look. Needs a window, so App calls it again on start.
        /// </summary>
        public static void ApplyStatusBar()
        {
            try
            {
                if (Application.Current?.Resources.TryGetValue("StatusBarColor", out var value) == true && value is Color color)
                {
                    StatusBar.SetColor(color);
                    StatusBar.SetStyle(StatusBarStyle.LightContent);
                }
            }
            catch (Exception ex)
            {
                // Before the activity exists there is no status bar to paint; OnStart repeats it.
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] status bar not painted: {ex.Message}");
            }
        }

        private static void Apply(Application app, SkinTheme theme, bool persist, AppTheme? phoneTheme = null)
        {
            var effective = theme == SkinTheme.System
                ? ((phoneTheme ?? app.PlatformAppTheme) == AppTheme.Dark ? SkinTheme.Dark : SkinTheme.Light)
                : theme;

            var merged = app.Resources.MergedDictionaries;

            // The first time, the dictionary to replace is the LightTheme App.xaml merged. Merged
            // by Source, it arrives as a plain ResourceDictionary wrapping the theme, not as one.
            _applied ??= merged.FirstOrDefault(d =>
                d is LightTheme or GrayTheme or DarkTheme or InfraredTheme
                || d.Source?.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) == true);

            if (_applied is not null)
                merged.Remove(_applied);

            _applied = Create(effective);
            merged.Add(_applied);

            Selected = theme;
            Effective = effective;

            // Only an explicit choice pins the native pieces; following the phone leaves them free.
            // Assigned only when it changes: assigning it at start, even Unspecified, stopped the
            // phone's light/dark changes from reaching the app while following the phone.
            var native = theme == SkinTheme.System
                ? AppTheme.Unspecified
                : (effective is SkinTheme.Light or SkinTheme.Gray ? AppTheme.Light : AppTheme.Dark);

            if (app.UserAppTheme != native)
                app.UserAppTheme = native;

            if (persist)
                Preferences.Default.Set(PreferenceKey, theme.ToString());

            ApplyStatusBar();
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        private static ResourceDictionary Create(SkinTheme theme) => theme switch
        {
            SkinTheme.Gray => new GrayTheme(),
            SkinTheme.Dark => new DarkTheme(),
            SkinTheme.Infrared => new InfraredTheme(),
            _ => new LightTheme()
        };

        private static SkinTheme ReadPreference()
        {
            var stored = Preferences.Default.Get(PreferenceKey, nameof(SkinTheme.System));
            return Enum.TryParse<SkinTheme>(stored, out var theme) ? theme : SkinTheme.System;
        }

        /// <summary>A colour of the active theme, for code that cannot use DynamicResource.</summary>
        public static Color GetColor(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
                ? color
                : fallback;
    }
}
