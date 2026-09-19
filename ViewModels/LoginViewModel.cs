using System.Windows.Input;
using Raphael.Driver.Services;
using Raphael.Driver.Models;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using CommunityToolkit.Mvvm.ComponentModel; // For ObservableObject y ObservableProperty
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using Raphael.Driver.Exceptions;
using Raphael.Driver.Views;         // For RelayCommand
using Raphael.Driver.Configuration;
using Raphael.Driver.Helpers;

namespace Raphael.Driver.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;
    //private readonly ISessionManagerService _sessionManager;

    [ObservableProperty]
    string _username;

    [ObservableProperty]
    string _password;

    [ObservableProperty]
    string _errorMessage;

    [ObservableProperty]
    bool _isBusy; // To indicate charging status

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordToggleIcon))] // Update icon when it changes
    bool _isPasswordMasked = true;

    [ObservableProperty]
    bool _rememberMe;

    // Application version displayed on the Login page
    public string Version => AppVersion.Display;

    /// <summary>
    /// The server this phone is pointed at, shown only when that is not production.
    /// </summary>
    /// <remarks>
    /// Empty in production on purpose. A banner that is there every day stops being read, and
    /// then the one phone somebody left on DEV looks exactly like the other thirty — writing
    /// real trips into the wrong database while nothing appears to be wrong.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEnvironmentBanner))]
    string _environmentBanner = ApiEnvironment.IsProduction
        ? string.Empty
        : $"{ApiEnvironment.Name.ToUpperInvariant()} — {ApiEnvironment.BaseUrl}";

    public bool ShowEnvironmentBanner => !string.IsNullOrEmpty(EnvironmentBanner);

    // For show/hide password icon
    public string PasswordToggleIcon => IsPasswordMasked ? "\uf070" : "\uf06e"; // eye-slash / eye

    public LoginViewModel(/*ISessionManagerService sessionManager*/)
    {
        //_sessionManager = sessionManager;
    }

    [RelayCommand]
    private async Task Login()
    {
        ErrorMessage = string.Empty; // Clean previous errors
        // Validation
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Please enter username and password.";
            //Application.Current.MainPage.DisplayAlert("Login failed.", "Authentication", "OK");
            return;
        }

        IsBusy = true; // Start charging indicator

        try
        {
            // ⚠️ Resolved, not constructed. `new AuthService(new GpsService())` built its own
            // HttpClient outside dependency injection, which is why signing in ignored both
            // the configured server address and the handler that identifies the build.
            var authService = ServiceHelper.GetService<IAuthService>();
            var result = await authService.LoginAsync(new LoginRequest { Username = Username, Password = Password });

            if (result != null && result.IsSuccess)
            {
                // Both halves together: the access token, and the credential that renews it
                // without sending the driver back to this screen. The refresh token goes to
                // SecureStorage, never to Preferences.
                await Services.Auth.TokenRenewal.StoreAsync(result.Token, result.RefreshToken);

                Preferences.Set("Username", Username);
                Preferences.Set("UserId", result.UserId);
                Preferences.Set("RememberMe", RememberMe);

                if (RememberMe)
                {
                    Preferences.Set("LastUsername", Username);
                }
                else
                {
                    Preferences.Remove("LastUsername");
                }

                //aqui no puede ser pq no se sabe la linea
                //await _sessionManager.CheckAndResumeGpsTrackingAsync();

                // Notifications on: hidden list for this driver, inbox, live hub and the push
                // token. It runs after the token is in Preferences because every one of those
                // needs it. A failure here must not block the sign in — the driver still has a
                // schedule to run — so the service swallows its own errors.
                await StartNotificationsAsync();

                // Navigate to HomePage 
                //await Shell.Current.GoToAsync("//HomePage");
                // Navigate to SchedulePage
                //await Shell.Current.GoToAsync("//SchedulePage");
                // Navigate to the SchedulePage. The prefix "//" resets the navigation stack
                // and set this page as the new root, restoring the menu.
                // The // prefix tells MAUI Shell: "Replace the current page (LoginPage) with this new page (SchedulePage) and make it the main page of the application."
                // Since SchedulePage is defined within your AppShell.xaml as a FlyoutItem, the Shell will automatically display the hamburger menu and navigation bar.
                await Shell.Current.GoToAsync($"//{nameof(SchedulePage)}");
                //await Shell.Current.GoToAsync($"//SchedulePage");
            }
            else
            {
                ErrorMessage = result?.Message ?? "Invalid credentials or unknown error.";
                await Application.Current.MainPage.DisplayAlert("Login Failed", "Invalid credentials", "OK");
            }
        }
        catch (ApiException apiEx)
        {
            Debug.WriteLine($"ApiException: {apiEx.Message} | Details: {apiEx.ErrorDetails} | StatusCode: {apiEx.StatusCode}");
            // More user-friendly message
            if (apiEx.Message.Contains("Connection error"))
            {
                ErrorMessage = "Could not connect to the server. Please check your connection and try again.";
            }
            else if (apiEx.StatusCode == System.Net.HttpStatusCode.Unauthorized || apiEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Incorrect username or password.";
            }
            else
            {
                ErrorMessage = $"Application error: {apiEx.Message}";
            }

            await Application.Current.MainPage.DisplayAlert("API error", ErrorMessage, "OK");
        }
        catch (Exception ex) // For any other unexpected exception
        {
            Debug.WriteLine($"Unexpected error in Login: {ex}");
            ErrorMessage = "An unexpected error occurred. Please try again later.";
            // Here is a good place to log the entire error (ex.ToString()) to a logging system.
            await Application.Current.MainPage.DisplayAlert("Error", ErrorMessage, "OK");
        }
        finally
        {
            IsBusy = false; // Stop charging indicator
        }

    }

    /// <summary>
    /// Brings up notifications for the driver who just signed in.
    /// </summary>
    private static async Task StartNotificationsAsync()
    {
        try
        {
            var session = ServiceHelper.GetService<NotificationSessionService>();

            if (session is not null)
                await session.StartAsync();

            // A push may have launched the app before there was a session to navigate with.
            await NotificationRouter.TryConsumeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoginViewModel: could not start notifications. {ex.Message}");
        }
    }

    [RelayCommand]
    private void TogglePasswordMask()
    {
        IsPasswordMasked = !IsPasswordMasked;
    }

    [RelayCommand]
    private async Task GoToMilanesPage()
    {
        try
        {
            Uri uri = new Uri("https://milanestransport.com/");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            // Handle errors when opening the browser
            Debug.WriteLine($"Error opening URL: {ex.Message}");
            ErrorMessage = "The link could not be opened.";
            await Application.Current.MainPage.DisplayAlert("Error", "The link could not be opened.", "OK");
        }
    }

    // ---------------------------------------------------------------- server settings

    private int _versionTaps;
    private DateTime _firstTapUtc;

    /// <summary>
    /// Seven taps on the version label opens the server dialog.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ It has to live on THIS screen. The Settings page is behind the sign-in, and signing
    /// in is the thing that needs an address — so a phone pointed at the wrong server could
    /// never be fixed from inside the application. That is what made the last move a visit to
    /// thirty-one phones.
    /// </para>
    /// <para>
    /// Seven taps, not a button: a driver must not find this by accident, and support can say
    /// "tap the version number seven times" down a telephone. It is the per-device escape
    /// hatch — moving the whole fleet is a DNS change, not thirty-one phone calls.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task TapVersion()
    {
        var now = DateTime.UtcNow;

        // The run resets if the taps are spread out, so ordinary fidgeting never reaches seven.
        if (_versionTaps == 0 || (now - _firstTapUtc).TotalSeconds > 5)
        {
            _versionTaps = 0;
            _firstTapUtc = now;
        }

        if (++_versionTaps < 7)
        {
            return;
        }

        _versionTaps = 0;
        await ShowServerDialogAsync();
    }

    private async Task ShowServerDialogAsync()
    {
        var page = Application.Current?.MainPage;

        if (page is null)
        {
            return;
        }

        var current = ApiEnvironment.IsOverridden
            ? $"{ApiEnvironment.Name} (set on this device)"
            : $"{ApiEnvironment.Name} (as built)";

        var choice = await page.DisplayActionSheet(
            $"Server: {current}\n{ApiEnvironment.BaseUrl}",
            "Cancel",
            null,
            "Production",
            "Development",
            "Other address...",
            "Use the built-in default");

        switch (choice)
        {
            case "Production":
                ApiEnvironment.SetOverride(ApiEnvironment.Production, ApiEnvironment.ProductionUrl);
                break;

            case "Development":
                ApiEnvironment.SetOverride(ApiEnvironment.Development, ApiEnvironment.DevelopmentUrl);
                break;

            case "Other address...":
                var typed = await page.DisplayPromptAsync(
                    "Server address",
                    "Full address, including https://",
                    initialValue: ApiEnvironment.BaseUrl,
                    keyboard: Keyboard.Url);

                if (string.IsNullOrWhiteSpace(typed))
                {
                    return;
                }

                // Checked before it is stored: a typo here leaves the phone unable to reach
                // anything, on the one screen that cannot be used to fix it.
                if (!Uri.TryCreate(typed.Trim(), UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    await page.DisplayAlert("Server address", "That is not a valid address.", "OK");
                    return;
                }

                ApiEnvironment.SetOverride("Custom", uri.ToString());
                break;

            case "Use the built-in default":
                ApiEnvironment.ClearOverride();
                break;

            default:
                return;
        }

        EnvironmentBanner = ApiEnvironment.IsProduction
            ? string.Empty
            : $"{ApiEnvironment.Name.ToUpperInvariant()} — {ApiEnvironment.BaseUrl}";

        await page.DisplayAlert(
            "Server changed",
            $"Now pointing at:\n{ApiEnvironment.BaseUrl}\n\nClose and reopen the application so every part of it picks this up.",
            "OK");
    }

    // Method to load preferences when starting the view
    public void OnAppearing()
    {
        RememberMe = Preferences.Get("RememberMe", false);

        if (RememberMe)
        {
            Username = Preferences.Get("LastUsername", string.Empty);
        }

        ErrorMessage = string.Empty; // Clear errors from previous sessions
    }
}