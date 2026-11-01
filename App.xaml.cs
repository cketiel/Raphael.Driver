using Raphael.Driver.Models;
using Raphael.Driver.Resources.Styles;
using Raphael.Driver.Services;
using Microsoft.Extensions.Configuration;


namespace Raphael.Driver
{
    public partial class App : Application
    {
        public App()
        {
            //AppConfig.Init();
            InitializeComponent();

            // Before the Shell: pages that read a colour with StaticResource take it at build time.
            ThemeManager.Initialize(this);

            // ⚠️ REMOVED: this used to write Preferences["ApiBaseUrl"] with a hard-coded
            // address on EVERY start, which meant any value stored there died at the next
            // launch -- so the preference could never have worked as an override, and the
            // address could only be changed by rebuilding. The server now comes from
            // Configuration/ApiEnvironment, resolved in MauiProgram before anything runs.
            // The animated splash plays first; the Shell is built when it ends, so its cost lands
            // on the splash's last frame instead of holding back the first one.
            MainPage = new Views.SplashPage(ShowShell);
        }

        /// <summary>
        /// Called by the splash when it ends or is tapped. Before the splash this ran in OnStart;
        /// Shell.Current is not null right after the assignment.
        /// </summary>
        private void ShowShell()
        {
            MainPage = new AppShell();

            // The splash painted the status bar its own colour; the theme takes it back.
            ThemeManager.ApplyStatusBar();
#if ANDROID
            (Platform.CurrentActivity as MainActivity)?.RestoreWindowBackground();
#endif
            Shell.Current.GoToAsync("///LoginPage");
        }

        /// <summary>
        /// Stops the Call me button asking the server while nobody can see it.
        /// </summary>
        protected override void OnSleep()
        {
            ServiceHelper.GetService<CallRequestStore>()?.Pause();
        }

        /// <summary>
        /// Whatever the office did with the request while the app was away is only on the server.
        /// </summary>
        protected override void OnResume()
        {
            var store = ServiceHelper.GetService<CallRequestStore>();

            if (store is not null)
                _ = store.ResumeAsync();
        }

    }

 

    /*public static class AppConfig
    {
        public static AppSettings Settings { get; private set; }

        public static void Init()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(FileSystem.AppDataDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Settings = config.Get<AppSettings>();
        }
    }*/
}
