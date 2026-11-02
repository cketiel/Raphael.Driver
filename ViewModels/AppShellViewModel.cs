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

        // RE-062: the INSPECTIONS menu item (which opened Samsara Driver) is gone: the vehicle is
        // inspected inside the Pull-out and the Pull-in.

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
        [RelayCommand]
        async Task SignOut()
        {
            // Resolved rather than constructed: the one built by hand had its own HttpClient
            // and knew nothing about the configured server.
            var _authService = Services.ServiceHelper.GetService<Services.IAuthService>();

            // Awaited. Calling it without awaiting is how it came to block the UI thread in
            // the first place.
            await _authService.LogoutAsync();

            // Logout logic
            //Preferences.Clear(); 
            //await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
        }
    }
}