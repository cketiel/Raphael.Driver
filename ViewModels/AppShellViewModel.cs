using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Services;
using Raphael.Driver.Views;

namespace Raphael.Driver.ViewModels
{
    public partial class AppShellViewModel : ObservableObject
    {
        private IProviderService ProviderService => ServiceHelper.GetService<IProviderService>();
        private IAuthService AuthService => ServiceHelper.GetService<IAuthService>();

        public AppShellViewModel()
        {           
            
        }

        [RelayCommand]
        private async Task OpenInspections()
        {
            bool opened = false;

            try
            {
#if ANDROID
                // LÓGICA NATIVA PARA ANDROID: Es la más segura para abrir por nombre de paquete
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                var intent = context.PackageManager.GetLaunchIntentForPackage("com.samsara.driver");
                if (intent != null)
                {
                    context.StartActivity(intent);
                    opened = true;
                }
#elif IOS
                // LÓGICA PARA iOS: Usa el esquema URL
                opened = await Launcher.Default.TryOpenAsync("samsara://");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Inspections: {ex.Message}");
                opened = false;
            }

            // Si no se pudo abrir (porque no está instalada o error)
            if (!opened)
            {
                string storeUrl = DeviceInfo.Platform == DevicePlatform.Android
                    ? "https://play.google.com/store/apps/details?id=com.samsara.driver"
                    : "https://apps.apple.com/us/app/samsara-driver/id1122606567";

                bool download = await Shell.Current.DisplayAlert(
                    "App Not Found",
                    "The Samsara Driver app is not installed. Would you like to download it from the store?",
                    "Download",
                    "Cancel");

                if (download)
                {
                    await Browser.Default.OpenAsync(storeUrl, BrowserLaunchMode.SystemPreferred);
                }
            }
        }

        [RelayCommand]
        private async Task SendSms()
        {
            try
            {
                // Get contact details from backend
                var provider = await ProviderService.GetContactProviderAsync();

                if (provider != null && !string.IsNullOrEmpty(provider.Phone))
                {
                    // Check if the device supports sending SMS
                    if (Sms.Default.IsComposeSupported)
                    {
                        // Open the native messaging app
                        await Sms.Default.ComposeAsync(new SmsMessage("", provider.Phone));
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Error", "SMS is not supported on this device.", "OK");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Company phone number not found.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not open messages: {ex.Message}", "OK");
            }
        }
        /*[RelayCommand]
        async Task SignOut()
        {
            // Usar el servicio inyectado
            _authService.Logout();
        }*/

        [RelayCommand]
        async Task SignOut()
        {
            // Resolved rather than constructed: the one built by hand had its own HttpClient
            // and knew nothing about the configured server.
            var _authService = Services.ServiceHelper.GetService<Services.IAuthService>();
            _authService.Logout();

            // Logout logic
            //Preferences.Clear(); 
            //await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
        }
    }
}