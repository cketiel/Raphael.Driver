using Raphael.Driver.ViewModels;
using Raphael.Driver.Views;
using Raphael.Driver.Helpers;

namespace Raphael.Driver
{
    public partial class AppShell : Shell
    {
        /// <summary>
        /// Gets the current application version displayed in the Shell.
        /// </summary>
        public string Version => AppVersion.Display;

        /// <summary>
        /// Which server this phone is talking to, shown in the flyout when it is not
        /// production. Empty on production, which is the normal case and needs no ornament.
        /// </summary>
        /// <remarks>
        /// ⚠️ It is shown HERE and not only on the sign-in screen, and the reason is the
        /// whole point of the warning. The login page is seen once, at the start of a shift,
        /// and then not again for hours; the flyout is what a driver opens all day. A phone
        /// somebody left pointed at DEV would otherwise look exactly like the other thirty
        /// for the rest of the shift, writing real trips into the wrong database with nothing
        /// on screen saying so. It sits next to the version for the same reason the version
        /// is there: it is what support asks for first.
        /// </remarks>
        public string EnvironmentBanner => Configuration.ApiEnvironment.IsProduction
            ? string.Empty
            : $"{Configuration.ApiEnvironment.Name.ToUpperInvariant()} — {Configuration.ApiEnvironment.BaseUrl}";

        public bool ShowEnvironmentBanner => !string.IsNullOrEmpty(EnvironmentBanner);

        public AppShell()
        {
            InitializeComponent();

            BindingContext = new AppShellViewModel();
            //BindingContext = Handler.MauiContext.Services.GetService<AppShellViewModel>()

            // Set the Shell title including the current application version.
            Title = $"Raphael Driver {Version}";

            //Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            //Routing.RegisterRoute(nameof(SchedulePage), typeof(SchedulePage));
            Routing.RegisterRoute(nameof(Views.PullOutDetailPage), typeof(Views.PullOutDetailPage));
            //Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(EventDetailPage), typeof(EventDetailPage));
            Routing.RegisterRoute(nameof(SignaturePage), typeof(SignaturePage));
            Routing.RegisterRoute(nameof(Views.VehicleInspectionPage), typeof(Views.VehicleInspectionPage));

            Routing.RegisterRoute(nameof(FutureDetailPage), typeof(FutureDetailPage));
            Routing.RegisterRoute(nameof(TripNotesPage), typeof(TripNotesPage)); // RE-054
            Routing.RegisterRoute(nameof(DocumentsPage), typeof(DocumentsPage)); // RE-065
            Routing.RegisterRoute(nameof(DocumentUploadPage), typeof(DocumentUploadPage));

            //Routing.RegisterRoute("LoginPage", typeof(Views.LoginPage));
            //Routing.RegisterRoute("HomePage", typeof(Views.HomePage));

            PaintChrome();
            // The menu rows draw only the glyph of each icon (Shell.ItemTemplate), in theme colours.
            Services.ThemeManager.ThemeChanged += (_, _) => PaintChrome();
        }

        /// <summary>
        /// Every page gets the theme's bar colours set on itself as it is shown. Set only on the
        /// Shell, a theme change left the bar of the theme the app started with: the Shell reads
        /// its appearance from the page first, and does not repaint for a resource it already used.
        /// </summary>
        private bool _warmed;

        /// <summary>
        /// Prepares the main pages after sign-in, one at a time, so the first tap finds them ready.
        /// A page that fails here is simply built when it is opened.
        /// </summary>
        public async void WarmUpPages()
        {
            if (_warmed) return;
            _warmed = true;
            var context = Handler?.MauiContext;
            if (context is null) return;
            string[] routes = ["DashboardPage", "SchedulePage", "ContactPage", "TodaySchedulePage", "NotificationsPage", "SettingsPage", "HistoryPage", "FutureSchedulePage"];
            foreach (var item in Items)
                foreach (var section in item.Items)
                    foreach (var content in section.Items)
                    {
                        if (!routes.Contains(content.Route)) continue;
                        await Task.Yield();
                        var start = System.Diagnostics.Stopwatch.GetTimestamp();
                        try
                        {
                            if (((IShellContentController)content).GetOrCreateContent() is Page page)
                                _ = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(page, context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"RAPHAEL-PERF: warm {content.Route} failed: {ex.Message}");
                        }
                        Console.WriteLine($"RAPHAEL-PERF: warm {content.Route} {(long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds} ms");
                    }
        }

        private long _navTicks;
        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);
            _navTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            var routeNow = args.Current?.Location?.OriginalString;
            var ticks = _navTicks;
            Dispatcher.Dispatch(() => Console.WriteLine($"RAPHAEL-PERF: {routeNow} {(long)System.Diagnostics.Stopwatch.GetElapsedTime(ticks).TotalMilliseconds} ms"));

            if (CurrentPage is Page page)
                PaintBar(page);
        }

        /// <summary>Top bar and side-menu background, in the colours of the active theme.</summary>
        private void PaintChrome()
        {
            PaintBar(this);

            if (CurrentPage is Page page)
                PaintBar(page);

            FlyoutBackgroundColor = Services.ThemeManager.GetColor("SurfaceColor", Colors.White);
        }

        private static void PaintBar(BindableObject target)
        {
            var bar = Services.ThemeManager.GetColor("NavBarColor", Color.FromArgb("#07577A"));
            var text = Services.ThemeManager.GetColor("NavBarTextColor", Colors.White);

            SetBackgroundColor(target, bar);
            SetForegroundColor(target, text);
            SetTitleColor(target, text);
        }

    }
}