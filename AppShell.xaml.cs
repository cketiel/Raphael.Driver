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
            Routing.RegisterRoute(nameof(TodaySchedulePage), typeof(TodaySchedulePage));
            Routing.RegisterRoute(nameof(Views.PullOutDetailPage), typeof(Views.PullOutDetailPage));
            //Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(EventDetailPage), typeof(EventDetailPage));
            Routing.RegisterRoute(nameof(SignaturePage), typeof(SignaturePage));

            Routing.RegisterRoute(nameof(FutureSchedulePage), typeof(FutureSchedulePage));
            Routing.RegisterRoute(nameof(FutureDetailPage), typeof(FutureDetailPage));
            Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
            Routing.RegisterRoute(nameof(ContactPage), typeof(ContactPage));

            //Routing.RegisterRoute("LoginPage", typeof(Views.LoginPage));
            //Routing.RegisterRoute("HomePage", typeof(Views.HomePage));

        }

    }
}