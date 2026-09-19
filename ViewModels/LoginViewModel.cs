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

        // Support's second way into the server dialog, and it is checked before the username
        // and password are validated so it works with the password box left empty.
        //
        // ⚠️ A typed codeword rather than another gesture, and the reason is the telephone.
        // The situation this exists for is somebody on a call with a driver who is pointed at
        // the wrong server; "type hash server as the user and press sign in" is one sentence
        // that works the same on every phone, with gloves on, in a van. A gesture has to be
        // described, practised and got right. It also cannot happen by accident: nobody types
        // this into a username box by mistake. The seven taps stay for whoever prefers them.
        if (string.Equals(Username?.Trim(), ServerCodeword, StringComparison.OrdinalIgnoreCase))
        {
            Username = string.Empty;
            TapHint = string.Empty;
            await ShowServerDialogAsync();
            return;
        }

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

                // Navigate to the SchedulePage. The prefix "//" resets the navigation stack
                // and sets this page as the new root, restoring the menu.
                await Shell.Current.GoToAsync($"//{nameof(SchedulePage)}");

                // Notifications on: hidden list for this driver, inbox, live hub and the push
                // token. It runs after the token is in Preferences because every one of those
                // needs it.
                //
                // ⚠️ Started, NOT awaited, and the difference is the whole complaint. The
                // comment here used to say "a failure here must not block the sign in" while
                // the line below it awaited the lot: an HTTP round trip, a SignalR connection,
                // the Android notification permission dialog and a Firebase token. On a fresh
                // install that is tens of seconds of a driver watching a spinner that looks
                // like it will never finish — and if the permission dialog goes unnoticed, it
                // genuinely never does.
                //
                // Nothing in there is needed to look at today's schedule, so none of it stands
                // between the driver and the schedule any more. It swallows its own errors, so
                // there is no exception to observe.
                _ = StartNotificationsAsync();
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

    /// <summary>
    /// Typed into the username box and followed by Sign in, this opens the server dialog
    /// instead of attempting to authenticate. See the check in <see cref="Login"/>.
    /// </summary>
    private const string ServerCodeword = "#server";

    private const int TapsRequired = 7;

    /// <summary>
    /// ⚠️ The gap allowed BETWEEN taps, not for the whole run. It used to be five seconds for
    /// all seven, which meant a driver tapping at a human pace ran out of time and had no way
    /// of knowing why — the count just silently went back to zero.
    /// </summary>
    private static readonly TimeSpan TapWindow = TimeSpan.FromSeconds(2);

    private int _versionTaps;
    private DateTime _lastTapUtc;

    /// <summary>
    /// Says how many taps are left once the run is clearly deliberate. Without it the whole
    /// thing is done blind: nothing on screen changes until the seventh tap, so a driver who
    /// miscounts cannot tell whether they are close or back at zero.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTapHint))]
    string _tapHint = string.Empty;

    public bool ShowTapHint => !string.IsNullOrEmpty(TapHint);

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

        // The run resets when the taps stop coming, so ordinary fidgeting never reaches seven.
        if (_versionTaps == 0 || now - _lastTapUtc > TapWindow)
        {
            _versionTaps = 0;
        }

        _lastTapUtc = now;
        _versionTaps++;

        if (_versionTaps < TapsRequired)
        {
            var left = TapsRequired - _versionTaps;

            // Silent for the first few, so a driver who taps the version twice out of habit
            // never learns there is anything here. It only speaks once the run is deliberate.
            TapHint = _versionTaps >= 3
                ? (left == 1 ? "1 more tap" : $"{left} more taps")
                : string.Empty;

            return;
        }

        _versionTaps = 0;
        TapHint = string.Empty;
        await ShowServerDialogAsync();
    }

    /// <summary>
    /// The server chooser. It closes when somebody chooses something, and not before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ There is no Cancel button, and that is deliberate. An action sheet is dismissed by
    /// tapping anywhere outside it, so the one stray finger that follows seven careful taps
    /// used to throw the whole thing away and send the driver back to tapping. Without a
    /// Cancel button an outside tap returns nothing at all, which is how this tells "dismissed
    /// by accident" from "chose to leave" — the latter is the <c>Close</c> option below.
    /// </para>
    /// <para>
    /// Bounded all the same. A window that genuinely cannot be closed is a worse trap than
    /// the one being fixed, and if the page is being torn down underneath us the sheet returns
    /// nothing every time.
    /// </para>
    /// </remarks>
    private async Task ShowServerDialogAsync()
    {
        var page = Application.Current?.MainPage;

        if (page is null)
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var current = ApiEnvironment.IsOverridden
                ? $"{ApiEnvironment.Name} (set on this device)"
                : $"{ApiEnvironment.Name} (as built)";

            var choice = await page.DisplayActionSheet(
                $"Server: {current}\n{ApiEnvironment.BaseUrl}",
                null,
                null,
                "Production",
                "Development",
                "Other address...",
                "Use the built-in default",
                "Close");

            // Dismissed without choosing. Seven taps to get here: a stray finger does not
            // undo them.
            if (string.IsNullOrEmpty(choice))
            {
                continue;
            }

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
                        // Backed out of the prompt, not out of the chooser.
                        continue;
                    }

                    // Checked before it is stored: a typo here leaves the phone unable to
                    // reach anything, on the one screen that cannot be used to fix it.
                    if (!Uri.TryCreate(typed.Trim(), UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    {
                        await page.DisplayAlert("Server address", "That is not a valid address.", "OK");
                        continue;
                    }

                    ApiEnvironment.SetOverride("Custom", uri.ToString());
                    break;

                case "Use the built-in default":
                    ApiEnvironment.ClearOverride();
                    break;

                default:
                    // "Close", and anything a platform might hand back that is not an option.
                    return;
            }

            EnvironmentBanner = ApiEnvironment.IsProduction
                ? string.Empty
                : $"{ApiEnvironment.Name.ToUpperInvariant()} — {ApiEnvironment.BaseUrl}";

            await page.DisplayAlert(
                "Server changed",
                $"Now pointing at:\n{ApiEnvironment.BaseUrl}\n\nClose and reopen the application so every part of it picks this up.",
                "OK");

            return;
        }
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