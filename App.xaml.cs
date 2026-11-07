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
            SendWhatIsWaiting();

            var outbox = ServiceHelper.GetService<Services.Offline.Outbox>();
            if (outbox is not null) outbox.Kept += OnKeptOnPhone;
        }

        private DateTime _lastKeptNotice = DateTime.MinValue;

        /// <summary>
        /// RE-062: the driver is told, without stopping them, that what they just did is safe on the
        /// phone and will reach the office by itself. Once every two minutes at most: a stretch without
        /// coverage keeps many changes and one notice says it.
        /// </summary>
        private void OnKeptOnPhone(object? sender, EventArgs e)
        {
            if (DateTime.UtcNow - _lastKeptNotice < TimeSpan.FromMinutes(2)) return;
            _lastKeptNotice = DateTime.UtcNow;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await CommunityToolkit.Maui.Alerts.Toast
                        .Make("No connection. Saved on the phone: it will be sent automatically.", CommunityToolkit.Maui.Core.ToastDuration.Long)
                        .Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Toast: {ex.Message}");
                }
            });
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
            ThemeManager.ApplyStatusBar();
            var store = ServiceHelper.GetService<CallRequestStore>();

            if (store is not null)
                _ = store.ResumeAsync();

            SendWhatIsWaiting();
        }

        /// <summary>
        /// RE-062: what was saved on the phone without coverage goes now, if it can. The outbox also
        /// tries on its own when the connection comes back and every half minute while it holds anything.
        /// </summary>
        public static void SendWhatIsWaiting()
        {
            _ = ServiceHelper.GetService<Services.Offline.Outbox>()?.FlushAsync();
            var factory = ServiceHelper.GetService<IHttpClientFactory>();
            if (factory is not null && Services.Offline.GpsBuffer.Count > 0)
                _ = Services.Offline.GpsBuffer.FlushAsync(factory.CreateClient("GpsClient"));
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
