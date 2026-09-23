 using CommunityToolkit.Maui;
using Raphael.Driver.Models;
using Raphael.Driver.Configuration;
using Raphael.Driver.Services;
using Raphael.Driver.Services.Auth;
using Raphael.Driver.ViewModels;
using Raphael.Driver.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Raphael.Driver
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {            
            var builder = MauiApp.CreateBuilder();
          
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fa-solid-900.ttf", "FontAwesomeSolid");
                });

            // --- API BASE URL ---
            //
            // Resolved once, here, before anything can make a request. It used to be a literal
            // on this line and in three other live places, one of which overwrote the address
            // this very registration supplies. See Configuration/ApiEnvironment.
            ApiEnvironment.Initialise();

            // --- DEPENDENCY INJECTION ---

            // Register the Interceptor (Token Handler)
            builder.Services.AddTransient<AuthHeaderHandler>();
            builder.Services.AddTransient<ClientVersionHandler>();

            // Register Services with HttpClient injected

            // AuthService: no auth interceptor, because signing in is what produces the token.
            // It DOES get the version handler: signing in is the first call anybody makes, and
            // leaving it out is what kept it out of the server's telemetry entirely.
            builder.Services.AddHttpClient<IAuthService, AuthService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            }).AddHttpMessageHandler<ClientVersionHandler>();

            // Services that DO require JWT Token (the interceptor is added)
            builder.Services.AddHttpClient<IScheduleService, ScheduleService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            }).AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            builder.Services.AddHttpClient<IRunService, RunService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            }).AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            builder.Services.AddHttpClient<IProviderService, ProviderService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            }).AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            builder.Services.AddHttpClient<INotificationApiService, NotificationApiService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            }).AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            // Fifteen seconds instead of the default hundred: a press that cannot get through has
            // to fail fast, because what comes after the failure is the offer to phone the office.
            builder.Services.AddHttpClient<ICallRequestService, CallRequestService>(client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            }).AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            // --- NOTIFICATIONS ---
            // Singletons: the bell in the navigation bar and the notifications page read the
            // same list and the same counter, so they cannot show different numbers.
            builder.Services.AddSingleton<HiddenNotificationStore>();
            builder.Services.AddSingleton<ConsumedSignalStore>();
            builder.Services.AddSingleton<IPushTokenProvider, PushTokenProvider>();
            builder.Services.AddSingleton<NotificationStore>();
            builder.Services.AddSingleton<RouteSignalCoordinator>();
            builder.Services.AddSingleton<INotificationHubService, NotificationHubService>();
            builder.Services.AddSingleton<NotificationSessionService>();

            // --- CALL REQUESTS ---
            // Singleton for the same reason as the notification store: the Call me button on every
            // page reads one state. The button's view model is one per button.
            builder.Services.AddSingleton<CallRequestStore>();
            builder.Services.AddTransient<CallRequestButtonViewModel>();

            // Other services that are not API or have special logic
            builder.Services.AddSingleton<IMapService, MapService>();
            builder.Services.AddSingleton<ISessionManagerService, SessionManagerService>();
            builder.Services.AddSingleton<App>();
            builder.Services.AddSingleton<IPhoneDialer>(PhoneDialer.Default);

            // GoogleMapsService was registered here. The app no longer talks to Google: travel
            // times come from api/routing/legs, which every service already reaches with the
            // driver's own token.

            // Services (Singleton because they do not save state and can be shared)
            /*builder.Services.AddSingleton<IScheduleService, ScheduleService>();          
            builder.Services.AddSingleton<IMapService, MapService>();
            builder.Services.AddSingleton<ISessionManagerService, SessionManagerService>();
            builder.Services.AddSingleton<App>();
            builder.Services.AddSingleton<IRunService, RunService>();

            builder.Services.AddSingleton<IPhoneDialer>(PhoneDialer.Default);
            builder.Services.AddSingleton<IProviderService, ProviderService>();
            builder.Services.AddSingleton<GoogleMapsService>();*/


            // ViewModels (Transient because each page should have its own instance)
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<ScheduleViewModel>();
            builder.Services.AddTransient<TodayScheduleViewModel>();
            builder.Services.AddTransient<PullOutDetailPage>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<EventDetailPageViewModel>();
            builder.Services.AddTransient<SignatureViewModel>();
            builder.Services.AddTransient<FutureScheduleViewModel>();
            builder.Services.AddTransient<FutureDetailViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<HistoryViewModel>();
            builder.Services.AddTransient<ContactViewModel>();

            // Singleton, unlike the rest: it subscribes to the notification store, which lives
            // for the whole session. A transient one would leave a dead subscription behind
            // every time the page was opened.
            builder.Services.AddSingleton<NotificationsViewModel>();

            // Views/Pages (Transient)
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<SchedulePage>();
            builder.Services.AddTransient<TodaySchedulePage>();
            builder.Services.AddTransient<PullOutDetailPageViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<EventDetailPage>();
            builder.Services.AddTransient<SignaturePage>();
            builder.Services.AddTransient<FutureSchedulePage>();
            builder.Services.AddTransient<FutureDetailPage>();
            builder.Services.AddSingleton<DashboardPage>();
            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<ContactPage>();
            builder.Services.AddTransient<NotificationsPage>();

            // Register a GPS-specific client that uses the Token interceptor
            builder.Services.AddHttpClient("GpsClient", client =>
            {
                client.BaseAddress = new Uri(ApiEnvironment.BaseUrl);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddHttpMessageHandler<ClientVersionHandler>();

            // We register the GPS service using a factory
            // which gets the static instance of MainActivity
            builder.Services.AddSingleton<IGpsService>(provider =>
            {
#if ANDROID
                
                return MainActivity.GpsService ?? new Raphael.Driver.Services.GpsServiceAndroid();
#elif IOS
    return new Raphael.Driver.Services.GpsServiceIos();
#else
    // Implementación por defecto para otras plataformas (Windows, etc.)
    return new GpsService(); 
#endif
            });


#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
