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
        public ObservableCollection<MapApp> AvailableMaps { get; } = new();

        /// <summary>The eleven schedule styles, the driver's one marked.</summary>
        public ObservableCollection<ScheduleStyleOption> ScheduleStyles { get; } =
            new(ScheduleStyleManager.All.Select(info => new ScheduleStyleOption(info, info.Style == ScheduleStyleManager.Current)));

        public SettingsViewModel(IMapService mapService)
        {
            _mapService = mapService;
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