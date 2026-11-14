using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Converters;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services;
using Raphael.Driver.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Raphael.Driver.ViewModels
{
    // We use QueryProperty to receive the ScheduleDto object during navigation
    [QueryProperty(nameof(Event), "EventDetail")]
    [QueryProperty(nameof(IsFirstEvent), "IsFirstEvent")]
    [QueryProperty(nameof(Events), "Events")]
    public partial class PullOutDetailPageViewModel : ObservableObject, IDisposable, IQueryAttributable
    {
        private readonly IScheduleService _scheduleService;
        private readonly IGpsService _gpsService;
        private readonly IMapService _mapService;
        private readonly IInspectionService _inspections;

        /// <summary>RE-062: what this garage event asks of the driver. Null until it is known.</summary>
        private VehicleInspectionRequirementDto? _requirement;

        /// <summary>The Provider requires the vehicle inspection here: it replaces Odometer (and the Pull-in signature).</summary>
        [ObservableProperty]
        private bool _inspectionRequired;

        /// <summary>The inspection of this event is done (sent, or kept on the phone to send).</summary>
        [ObservableProperty]
        private bool _isInspected;

        [ObservableProperty]
        private bool _showInspectionAction;

        [ObservableProperty]
        private ObservableCollection<ScheduleDto> _events;

        [ObservableProperty]
        private ScheduleDto _event;

        [ObservableProperty]
        private Color _eventColor;

        private readonly ScheduleColorConverter _colorConverter = new();

        [ObservableProperty]
        private bool _isOdometerEntered;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OdometerOrPerformActionCommand))] 
        private bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotTracking))]
        private bool isTracking;

        public bool IsNotTracking => !IsTracking;

        [ObservableProperty]
        private bool _isFirstEvent;

        // Controls the visibility of the Odometer/Perform action row
        [ObservableProperty]
        private bool _isPrimaryActionVisible;

        // Properties for dynamic texts in the UI
        [ObservableProperty]
        private string _pageTitle;

        [ObservableProperty]
        private string _mapActionText;

        [ObservableProperty]
        private bool _isSignatureEntered;

        [ObservableProperty]
        private bool _showOdometerAction;

        [ObservableProperty]
        private bool _showSignatureAction;

        [ObservableProperty]
        private bool _showPerformAction;

        /// <summary>
        /// The same rows the page draws for Legacy, as a list, for the schedule styles' action list
        /// (Controls/ScheduleCards/ActionListView). Same commands, same order; rebuilt whenever the
        /// rows above change.
        /// </summary>
        public ObservableCollection<EventAction> Actions { get; } = new();

        public PullOutDetailPageViewModel(IScheduleService scheduleService, IGpsService gpsService, IMapService mapService, IInspectionService inspections)
        {
            _scheduleService = scheduleService;
            _inspections = inspections;
            _gpsService = gpsService;
            _mapService = mapService;

            // Subscribe to service status changes
            if (_gpsService != null)
            {
                _gpsService.IsTrackingChanged += OnGpsTrackingChanged;
                // Synchronize initial state
                IsTracking = _gpsService.IsTracking;
            }
        }

        partial void OnIsFirstEventChanged(bool value) => UpdateUIState();
        //partial void OnEventChanged(ScheduleDto value) => UpdateUIState();

        private void UpdateUIState()
        {
            if (Event == null) return;

            // TÍTULOS Y COLORES
            PageTitle = Event.Name;
            MapActionText = $"Maps - {Event.Name} Address";
            EventColor = (Color)_colorConverter.Convert(Event, typeof(Color), null, System.Globalization.CultureInfo.CurrentCulture);

            // LÓGICA DE VISIBILIDAD CRÍTICA:
            // Se muestra si es Pull-out (que siempre suele ser el primero)
            // O si es Pull-in Y es el primero de la lista (porque ya se completó el resto)
            IsPrimaryActionVisible = Event.Name == "Pull-out" || (Event.Name == "Pull-in" && IsFirstEvent);

            // Refrescar los estados internos de los botones (Odometer/Signature/Perform)
            _ = RefreshStates();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // RE-062: back from the inspection. Its odometer is this event's reading.
            if (query.TryGetValue("InspectionDone", out var odometer) && odometer is long reading)
            {
                if (Event != null) Event.Odometer = reading;
                IsInspected = true;
                _ = RefreshStates();
            }

            if (query.TryGetValue("SignatureSaved", out var saved) && (bool)saved)
            {

                if (Event != null)
                {

                    Event.PassengerSignature = new byte[] { 1 };
                }
                _ = RefreshStates();
            }
        }

        /*public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("SignatureSaved", out var saved) && (bool)saved)
            {
                // Forzamos la recarga del objeto o simplemente marcamos como firmado
                IsSignatureEntered = true;
                _ = RefreshStates();
            }
        }*/


        /*public async Task ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("SignatureSaved"))
            {
                IsSignatureEntered = true;
                await RefreshStates();
            }
        }*/

        private async Task RefreshStates()
        {
            if (Event == null) return;

            IsOdometerEntered = Event.Odometer != null && Event.Odometer > -1;
            // We simulate the signature verification by looking if it already exists in the object
            IsSignatureEntered = !string.IsNullOrEmpty(Event.PassengerSignature?.ToString());

            if (InspectionRequired)
            {
                // RE-062: Inspection -> Perform. The inspection carries the odometer and, at Pull-in,
                // the driver's signature, which then certifies that inspection only.
                ShowInspectionAction = !IsInspected;
                ShowOdometerAction = false;
                ShowSignatureAction = false;
                ShowPerformAction = IsInspected;
            }
            else if (Event.Name == "Pull-in")
            {
                // Pull-in sequence: Odometer -> Signature -> Perform
                ShowOdometerAction = !IsOdometerEntered;
                ShowSignatureAction = IsOdometerEntered && !IsSignatureEntered;
                ShowPerformAction = IsOdometerEntered && IsSignatureEntered;
            }
            else // Pull-out
            {
                // Normal sequence: Odometer -> Perform
                ShowOdometerAction = !IsOdometerEntered;
                ShowSignatureAction = false;
                ShowPerformAction = IsOdometerEntered;
            }

            // Reporting changes to ensure UI IsVisible is triggered
            OnPropertyChanged(nameof(ShowOdometerAction));
            OnPropertyChanged(nameof(ShowSignatureAction));
            OnPropertyChanged(nameof(ShowPerformAction));

            RebuildActions();
        }

        private void RebuildActions()
        {
            Actions.Clear();

            if (IsPrimaryActionVisible)
            {
                if (ShowInspectionAction)
                    Actions.Add(new EventAction { IsMain = true, Text = "Vehicle Inspection", IconGlyph = "\uf46d", Command = GoToInspectionCommand });
                if (ShowOdometerAction)
                    Actions.Add(new EventAction { IsMain = true, Text = "Odometer", IconGlyph = "", Command = OdometerOrPerformActionCommand });
                if (ShowSignatureAction)
                    Actions.Add(new EventAction { IsMain = true, Text = "Driver Signature", IconGlyph = "", Command = GoToSignatureCommand });
                if (ShowPerformAction)
                    Actions.Add(new EventAction { IsMain = true, Text = "Perform", IconGlyph = "", Command = OdometerOrPerformActionCommand });
            }

            Actions.Add(new EventAction { Text = MapActionText, IconGlyph = "", Command = GoToMapsCommand });
        }
        [RelayCommand]
        private async Task GoToInspection()
        {
            if (IsBusy || Event is null) return;

            try
            {
                _requirement ??= await _inspections.GetRequirementAsync(Event.Id, Event.Name);
            }
            catch (InspectionRefusedException refused)
            {
                // RE-067: the server said no; a checklist it will refuse is never handed out.
                await Shell.Current.DisplayAlert("Inspection not available", refused.Message, "OK");
                return;
            }

            await Shell.Current.GoToAsync(nameof(VehicleInspectionPage), new Dictionary<string, object>
            {
                { "Requirement", _requirement },
                { "EventDetail", Event }
            });
        }

        /// <summary>
        /// RE-062: asks once per event whether the Provider requires the inspection here. Without
        /// coverage the phone's copy answers (see InspectionService).
        /// </summary>
        private async Task LoadRequirementAsync(ScheduleDto value)
        {
            try
            {
                var requirement = await _inspections.GetRequirementAsync(value.Id, value.Name);
                if (Event?.Id != value.Id) return;

                _requirement = requirement;
                InspectionRequired = requirement.Required;
                IsInspected = requirement.Existing is not null || _inspections.IsDoneOnPhone(value.Id);
                if (requirement.Existing is not null && value.Odometer is null) value.Odometer = requirement.Existing.Odometer;
                await RefreshStates();
            }
            catch (InspectionRefusedException refused)
            {
                // RE-067: the server answered and does not offer this inspection to this driver. The
                // Pull-out goes on as without the module; if the server does require it, Perform comes
                // back with 409 inspection_required and the reason is shown then.
                Debug.WriteLine($"Inspection requirement refused ({refused.Status})");
                if (Event?.Id != value.Id) return;
                _requirement = null;
                InspectionRequired = false;
                await RefreshStates();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Inspection requirement: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task GoToSignature()
        {
            if (IsBusy) return;

            await Shell.Current.GoToAsync(nameof(SignaturePage), new Dictionary<string, object>
        {
            { "ScheduleId", Event.Id },
            { "IsDriverSignature", true } // This activates the legal text
        });
        }


        [RelayCommand(CanExecute = nameof(CanExecuteAction))]
        private async Task OdometerOrPerformAction()
        {
            // The whole row of the Legacy style answers to this command: with the inspection pending,
            // a tap anywhere on it opens the inspection, never the old odometer prompt.
            if (ShowInspectionAction)
            {
                await GoToInspection();
                return;
            }

            if (IsOdometerEntered && (!InspectionRequired || IsInspected))
            {
                // Logic for when the user presses "Perform"
                await PerformAction();
            }
            else
            {
                // Logic for when the user presses "Odometer"
                await EnterOdometer();
            }
        }

        private bool CanExecuteAction() => !IsBusy;

        
        private async Task<bool> CheckAndRequestLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
                return true;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // On iOS, if the user denies permission, they cannot be asked again.
                await Shell.Current.DisplayAlert("Permission Required", "Location permission was denied. Please activate it in the application settings.", "OK");
                return false;
            }

            // Request permission from the user
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            return status == PermissionStatus.Granted;
        }

        private void OnGpsTrackingChanged(bool isTracking)
        {
            // The service notifies us that its status has changed
            // We update the property in the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsTracking = isTracking;
            });
        }

        public void Dispose()
        {
            // Unsubscribe from the event to avoid memory leaks
            if (_gpsService != null)
            {
                _gpsService.IsTrackingChanged -= OnGpsTrackingChanged;
            }
        }       

        private async Task EnterOdometer()
        {
            // To avoid double clicks.
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var result = await Shell.Current.DisplayPromptAsync(
                    "Odometer",
                    "Enter Odometer Reading.",
                    "OK",
                    "Cancel",
                    keyboard: Keyboard.Numeric
                );

                if (string.IsNullOrWhiteSpace(result)) return;

                if (long.TryParse(result, out long odometerValue) && odometerValue > -1)
                {
                    Event.Odometer = odometerValue;

                    bool success = await _scheduleService.UpdateScheduleAsync(Event);

                    if (success)
                    {
                        //IsOdometerEntered = true;
                        await RefreshStates();
                        await Shell.Current.DisplayAlert("Success", "Odometer reading has been saved.", "OK");
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Error", "Could not save the odometer reading. Please try again.", "OK");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Invalid Input", "Please enter a valid number for the odometer.", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PerformAction()
        {
            if (IsBusy) return;

            IsBusy = true;

            try
            {
                
                if (Event.Name == "Pull-out")
                {
                    var hasPermission = await CheckAndRequestLocationPermissionAsync();
                    if (!hasPermission)
                    {
                        // The user did not give permission, we cannot continue.
                        await Shell.Current.DisplayAlert("Permission Required", "Tracking cannot be started without location permission.", "OK");
                        return; // We leave the method
                    }
                }

                TimeSpan? oldEta = Event.ETA;
                Event.Perform = DateTime.Now.TimeOfDay;
                Event.Performed = true;
                Event.ETA = Event.Perform;

                // Through the outbox (RE-062): with no coverage it is kept and sent later, and the
                // driver carries on. Refused with 409, the Provider requires the inspection first.
                var result = await _scheduleService.SaveScheduleAsync(Event);
                bool success = result.Accepted;

                if (result.Status == 409 && (result.Body ?? "").Contains("inspection_required"))
                {
                    Event.Perform = null;
                    Event.Performed = false;
                    Event.ETA = oldEta;
                    InspectionRequired = true;
                    IsInspected = false;
                    await RefreshStates();
                    await Shell.Current.DisplayAlert("Vehicle inspection",
                        "Your company requires the vehicle inspection before this step.", "OK");
                    return;
                }

                if (success)
                {
                    if (Event.Name == "Pull-out")
                    {
                        var pendingEvents = Events.ToList();
                        await _scheduleService.UpdateNextSchedulesETAsAsync(pendingEvents, Event.Id, Event.Perform.Value);

                        _gpsService.StartTracking(Event.VehicleRouteId);
                    }
                    else if (Event.Name == "Pull-in")
                    {
                        _gpsService.StopTracking();
                    }
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    Event.Perform = null;
                    Event.Performed = false;
                    Event.ETA = oldEta;
                    await Shell.Current.DisplayAlert("Error", "The action could not be performed. Please check your connection and try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error performing action: {ex.Message}");
                Event.Performed = false;
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /*private async Task PerformAction()
        {
            if (IsBusy) return; // Prevent user from clicking multiple times

            IsBusy = true;

            try
            {               
                Event.Performed = true; 

                bool success = await _scheduleService.UpdateScheduleAsync(Event);

                if (success)
                {                  
                    if (Event.Name == "Pull-out")
                    {
                        // The ViewModel just gives the order. The service takes care of the rest.
                        _gpsService?.StartTracking(Event.VehicleRouteId);

                        //StartGpsTracking();
                        //Debug.WriteLine("GPS tracking started due to Pull-out event.");
                    }
                    else if (Event.Name == "Pull-in")
                    {
                        _gpsService?.StopTracking();
                        //StopGpsTracking();
                        //Debug.WriteLine("GPS tracking stopped due to Pull-in event.");
                    }
                    // We navigate back in the navigation stack.
                    // ".." is the shell syntax for going to the previous page.
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    // If it fails, we inform the user and revert the change locally
                    // to maintain state consistency.
                    Event.Performed = false;
                    await Shell.Current.DisplayAlert("Error", "Could not perform the action. Please check your connection and try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error performing action: {ex.Message}");
                Event.Performed = false; // Rollback in case of exception
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                // We ensure that the 'IsBusy' status is always reset
                IsBusy = false;
            }

            // Alerta de confirmación
            //await Shell.Current.DisplayAlert("Action Performed", "The 'Perform' action has been completed successfully.", "OK");
        }*/

        [RelayCommand]
        private async Task GoToOdometer()
        {
            // Shows the text entry dialog
            var result = await Shell.Current.DisplayPromptAsync(
                "Odometer",
                "Enter Odometer Reading.",
                "OK",
                "Cancel",
                keyboard: Keyboard.Numeric // This displays a numeric keypad to the user
            );

            // The user pressed "Cancel" or left the field empty
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            // We validate that the input is a number
            if (long.TryParse(result, out long odometerValue))
            {
                // The object in the ViewModel is updated
                Event.Odometer = odometerValue;

                // Service is called to save changes to the API
                bool success = await _scheduleService.UpdateScheduleAsync(Event);

                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Odometer reading has been saved.", "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Could not save the odometer reading. Please try again.", "OK");
                }
            }
            else
            {
                // The user entered something that is not a valid number
                await Shell.Current.DisplayAlert("Invalid Input", "Please enter a valid number for the odometer.", "OK");
            }

            // TODO: Implementar navegación a la página del Odómetro
            /*await Shell.Current.DisplayAlert("Función no implementada", "La navegación al odómetro aún no está disponible.", "OK");
            Debug.WriteLine("Navegando a la página del Odómetro...");*/
        }

        [RelayCommand]
        private async Task GoToMaps()
        {
            if (IsBusy || Event is null) return;

            IsBusy = true;
            try
            {
                await _mapService.LaunchNavigationAsync(Event.ScheduleLatitude, Event.ScheduleLongitude, Event.Address);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MapsCommand: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred while trying to open maps.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CopyAddress()
        {
            if (Event != null && !string.IsNullOrWhiteSpace(Event.Address))
            {
                await Clipboard.SetTextAsync(Event.Address);
                await Shell.Current.DisplayAlert("Copied", "The address has been copied to the clipboard.", "OK");
            }
        }

        [RelayCommand]
        private async Task GoBack()
        {
            // If there is an operation in progress, we do not allow returning to avoid inconsistent states.
            if (IsBusy)
            {
                return;
            }

            // We use ".." to navigate to the previous page in the navigation stack.
            await Shell.Current.GoToAsync("..");
        }

        // This method is automatically fired when the 'Event' property receives a value
        partial void OnEventChanged(ScheduleDto value)
        {
            if (value != null)
            {
                PageTitle = value.Name; // "Pull-out" o "Pull-in"
                MapActionText = $"Maps - {value.Name} Address";

                // Determine if the main action (Odometer/Perform) should be visible.
                //    - A "Pull-out" is always the first event, so it is always actionable.
                //    - A "Pull-in" is only actionable if it is the only event left in the list
                //      (which means IsFirstEvent will be true for it).
                IsPrimaryActionVisible = value.Name == "Pull-out" || (value.Name == "Pull-in" && IsFirstEvent);

                EventColor = (Color)_colorConverter.Convert(value, typeof(Color), null, System.Globalization.CultureInfo.CurrentCulture);
                IsOdometerEntered = value.Odometer != null && value.Odometer > -1;

                _requirement = null;
                IsInspected = _inspections.IsDoneOnPhone(value.Id);
                _ = RefreshStates();
                _ = LoadRequirementAsync(value);
            }
            else
            {               
                EventColor = Colors.Gray;
                IsOdometerEntered = false;
            }
        }

        public void StartGpsTracking() 
        {
            if (_gpsService.IsTracking) return;

            _gpsService.StartTracking(Event.VehicleRouteId);
            IsTracking = _gpsService.IsTracking;

        }

        public void StopGpsTracking()
        {
            if (!_gpsService.IsTracking) return;

            _gpsService.StopTracking();
            IsTracking = _gpsService.IsTracking;
        }

    }
}