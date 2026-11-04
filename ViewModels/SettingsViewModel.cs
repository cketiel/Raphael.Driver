using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Models;
using Raphael.Driver.Services;
using System.Collections.ObjectModel;

namespace Raphael.Driver.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IMapService _mapService;
        private readonly IComplianceService _compliance;
        private readonly IAccountService _account;

        // The name comes with the documents; kept for the session so Settings opens with it.
        private static string? _knownName;
        private static string? _vehicleName;
        private static string? _nameOfUser;
        public ObservableCollection<MapApp> AvailableMaps { get; } = new();

        /// <summary>The eleven schedule styles, the driver's one marked.</summary>
        public ObservableCollection<ScheduleStyleOption> ScheduleStyles { get; } =
            new(ScheduleStyleManager.All.Select(info => new ScheduleStyleOption(info, info.Style == ScheduleStyleManager.Current)));

        public SettingsViewModel(IMapService mapService, IComplianceService compliance, IAccountService account)
        {
            _mapService = mapService;
            _compliance = compliance;
            _account = account;
            Username = Preferences.Get("Username", string.Empty);
            if (_nameOfUser != Username) _knownName = _vehicleName = null;
            ShowProfile();
        }

        // ---- The header ------------------------------------------------------------------

        public string Username { get; }
        public string AppVersionText => $"Raphael Driver {Helpers.AppVersion.Current}";
        [ObservableProperty] private string _driverName = "Driver";
        [ObservableProperty] private string _initials = "";
        [ObservableProperty] private string? _vehicleLine;
        public bool HasVehicleLine => !string.IsNullOrEmpty(VehicleLine);
        partial void OnVehicleLineChanged(string? value) => OnPropertyChanged(nameof(HasVehicleLine));

        [RelayCommand]
        private async Task LoadProfileAsync()
        {
            if (_knownName is not null) return;
            var result = await _compliance.GetMineAsync();
            if (!result.Ok || result.Value is null) return;
            _knownName = result.Value.Driver.SubjectName;
            _vehicleName = result.Value.Vehicle?.SubjectName;
            _nameOfUser = Username;
            ShowProfile();
        }

        private void ShowProfile()
        {
            DriverName = _knownName ?? (string.IsNullOrEmpty(Username) ? "Driver" : Username);
            Initials = DocumentsViewModel.InitialsOf(DriverName);
            VehicleLine = _vehicleName;
        }

        // ---- The tabs ---------------------------------------------------------------------

        /// <summary>account, appearance or navigation.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAccount), nameof(IsAppearance), nameof(IsNavigation))]
        private string _tab = "account";
        public bool IsAccount => Tab == "account";
        public bool IsAppearance => Tab == "appearance";
        public bool IsNavigation => Tab == "navigation";

        [RelayCommand]
        private void SelectTab(string tab) => Tab = tab;

        // ---- Change password (the sheet) ---------------------------------------------------

        public const int MinPasswordLength = 8;

        [ObservableProperty] private bool _isPasswordSheetOpen;
        [ObservableProperty] private string _currentPassword = string.Empty;
        [ObservableProperty] private string _newPassword = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;
        [ObservableProperty] private bool _revealPasswords;
        [ObservableProperty] private string? _passwordError;
        [ObservableProperty] private bool _isSavingPassword;
        public bool HidePasswords => !RevealPasswords;
        partial void OnRevealPasswordsChanged(bool value) => OnPropertyChanged(nameof(HidePasswords));

        [RelayCommand]
        private void OpenPasswordSheet()
        {
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
            PasswordError = null;
            RevealPasswords = false;
            IsPasswordSheetOpen = true;
        }

        [RelayCommand]
        private void ClosePasswordSheet()
        {
            if (IsSavingPassword) return;
            IsPasswordSheetOpen = false;
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        }

        [RelayCommand]
        private void ToggleReveal() => RevealPasswords = !RevealPasswords;

        [RelayCommand]
        private async Task SavePasswordAsync()
        {
            PasswordError =
                string.IsNullOrEmpty(CurrentPassword) ? "Type your current password."
                : NewPassword.Length < MinPasswordLength ? $"The new password needs at least {MinPasswordLength} characters."
                : NewPassword != ConfirmPassword ? "The new password and its confirmation do not match."
                : NewPassword == CurrentPassword ? "The new password has to be different from the current one."
                : null;
            if (PasswordError is not null) return;

            IsSavingPassword = true;
            try
            {
                PasswordError = await _account.ChangePasswordAsync(CurrentPassword, NewPassword, ConfirmPassword);
            }
            finally
            {
                IsSavingPassword = false;
            }
            if (PasswordError is not null) return;

            ClosePasswordSheet();
            await Shell.Current.DisplayAlert("Password changed", "Use the new password the next time you sign in.", "OK");
        }

        /// <summary>Draws Today's Schedule and the stop screens in <paramref name="style"/> from now on, on this phone.</summary>
        [RelayCommand]
        private void UseScheduleStyle(ScheduleStyle style)
        {
            ScheduleStyleManager.Select(style);
            foreach (var option in ScheduleStyles)
                option.IsSelected = option.Style == style;
        }

        /// <summary>Drops the driver's theme choice and goes back to the phone's light/dark setting.</summary>
        [RelayCommand]
        private void FollowPhoneTheme() => ThemeManager.Select(SkinTheme.System);

        [RelayCommand]
        private async Task LoadMapAppsAsync()
        {
            if (AvailableMaps.Any()) return;

            // --- CÓDIGO DE PRUEBA PARA VERIFICAR LA UI ---           
            /*AvailableMaps.Add(new MapApp { Name = "Google Maps", Icon = "google_maps_icon.png", IsSelected = true, Scheme = "comgooglemaps://" });
            AvailableMaps.Add(new MapApp { Name = "Waze", Icon = "waze_icon.png", IsSelected = false, Scheme = "waze://" });
            AvailableMaps.Add(new MapApp { Name = "Apple Maps", Icon = "apple_maps_icon.png", IsSelected = false, Scheme = "maps://" });*/

            
            
            var apps = await _mapService.GetAvailableMapAppsAsync();
            var defaultScheme = _mapService.GetDefaultMapAppScheme();

            foreach (var app in apps)
            {
                if (app.Scheme == defaultScheme)
                {
                    app.IsSelected = true;
                }
                AvailableMaps.Add(app);
            }
            
        }

        [RelayCommand]
        private void SelectMap(MapApp selectedApp)
        {
            // Save to prevent it from being executed if the user touches the already selected item.
            if (selectedApp == null || selectedApp.IsSelected)
            {
                return;
            }

            // Deselect the previous item in the data model
            var previouslySelected = AvailableMaps.FirstOrDefault(m => m.IsSelected);
            if (previouslySelected != null)
            {
                previouslySelected.IsSelected = false;
            }

            // Select the new one in the data model
            selectedApp.IsSelected = true;

            // Save preference 
            _mapService.SetDefaultMapAppScheme(selectedApp.Scheme);
        }
    }
}