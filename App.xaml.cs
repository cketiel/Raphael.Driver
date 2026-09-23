using Raphael.Driver.Models;
using Raphael.Driver.Resources.Styles;
using Raphael.Driver.Services;
using Microsoft.Extensions.Configuration;


namespace Raphael.Driver
{
    public partial class App : Application
    {
        private readonly DarkTheme _darkThemeDictionary;
        public App()
        {
            //AppConfig.Init();
            InitializeComponent();

            // ⚠️ REMOVED: this used to write Preferences["ApiBaseUrl"] with a hard-coded
            // address on EVERY start, which meant any value stored there died at the next
            // launch -- so the preference could never have worked as an override, and the
            // address could only be changed by rebuilding. The server now comes from
            // Configuration/ApiEnvironment, resolved in MauiProgram before anything runs.
            MainPage = new AppShell();
            // Go directly to LoginPage on startup
            //Shell.Current.GoToAsync("//LoginPage");

            _darkThemeDictionary = new DarkTheme();

            // Suscribirse al evento de cambio de tema del sistema operativo.
            Current.RequestedThemeChanged += OnRequestedThemeChanged;

            // Establecer el tema correcto cuando la aplicación se inicia por primera vez.
            LoadTheme(Current.RequestedTheme);

        }

        /// <summary>
        /// This method is called by the framework AFTER the app has been initialized
        /// and MainPage has been assigned. Shell.Current is GUARANTEED not to be null here.
        /// </summary>
        protected override void OnStart()
        {           
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

        private void OnRequestedThemeChanged(object sender, AppThemeChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadTheme(e.RequestedTheme);
            });
        }

        private void LoadTheme(AppTheme theme)
        {
            var mergedDictionaries = Current.Resources.MergedDictionaries;

            if (mergedDictionaries.Contains(_darkThemeDictionary))
            {
                mergedDictionaries.Remove(_darkThemeDictionary);
            }

            if (theme == AppTheme.Dark)
            {
                // Como _darkThemeDictionary ya es una instancia completa, simplemente la añadimos.
                mergedDictionaries.Add(_darkThemeDictionary);
            }
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
