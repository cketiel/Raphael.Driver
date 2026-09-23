
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Converters;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services;
using Raphael.Driver.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.Communication;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Raphael.Driver.ViewModels
{
    [QueryProperty(nameof(Event), "EventDetail")]
    [QueryProperty(nameof(SignatureSaved), "SignatureSaved")] // Receive the result of the signature page
    [QueryProperty(nameof(IsFirstEvent), "IsFirstEvent")]
    [QueryProperty(nameof(Events), "Events")]
    public partial class EventDetailPageViewModel : ObservableObject
    {
        private readonly IScheduleService _scheduleService;
        private readonly IGpsService _gpsService;
        private readonly IMapService _mapService;
        private readonly IPhoneDialer _phoneDialer;
        private readonly IProviderService _providerService;

        [ObservableProperty]
        private ObservableCollection<ScheduleDto> _events;

        [ObservableProperty]
        private bool _isFirstEvent;

        // The selected event we received from the previous page
        [ObservableProperty]
        private ScheduleDto _event;

        // The collection of actions to display in the UI
        public ObservableCollection<EventAction> Actions { get; } = new();

        // The state that controls which actions are displayed
        [ObservableProperty]
        private bool _hasArrived;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private Color _eventColor;

        private readonly ScheduleColorConverter _colorConverter = new();

        [ObservableProperty]
        private bool _hasSignature;

        // Property that is set when returning from the signature page
        [ObservableProperty]
        private bool _signatureSaved;

        public EventDetailPageViewModel(IScheduleService scheduleService, IGpsService gpsService, IMapService mapService, IPhoneDialer phoneDialer, IProviderService providerService)
        {
            _scheduleService = scheduleService;
            _gpsService = gpsService;   
            _mapService = mapService;
            _phoneDialer = phoneDialer;
            _providerService = providerService;
        }

        // Called automatically when the 'Event' property receives a value
        partial void OnEventChanged(ScheduleDto value)
        {
            if (value != null)
            {
                EventColor = (Color)_colorConverter.Convert(value, typeof(Color), null, System.Globalization.CultureInfo.CurrentCulture);
                // We initialize the status based on whether the arrival has already been recorded
                HasArrived = value.Arrive.HasValue;
                HasSignature = value.PassengerSignature != null; // Initialize signature status
                BuildActionsList();
                
            }
            else
            {
                EventColor = Colors.Gray;

            }
        }

        // This method will fire when the IsFirstEvent property is set by the navigation.
        partial void OnIsFirstEventChanged(bool value)
        {
            // We call the logic again to make sure it is rebuilt
            // the list of actions in case this property was the last to arrive.
            BuildActionsList();
        }

        // Method that fires when SignatureSaved changes
        partial void OnSignatureSavedChanged(bool value)
        {
            if (value)
            {
                HasSignature = true;
                BuildActionsList(); // Refresh action list
            }
        }

        /// <summary>
        /// Build or rebuild the list of actions visible to the user.
        /// </summary>
        private void BuildActionsList()
        {
            // If the Event has not yet been set, we exit to avoid errors.
            // This is crucial for when OnIsFirstEventChanged is executed first.
            if (Event is null) return;

            Actions.Clear();

            // Now, when this code is executed, we are guaranteed that
            // both 'Event' and 'IsFirstEvent' have their correct values.
            // We only show the main actions (Arrive, Signature, Perform, Cancel)
            // IF AND ONLY IF this is the first event on the list.
            if (IsFirstEvent)
            {
                if (!HasArrived)
                {
                    // STATUS 1: The driver has not arrived yet.
                    // The action is "Arrive", regardless of whether it is Pickup or Dropoff.
                    Actions.Add(new EventAction { Text = "Arrive", IconGlyph = "", Command = ArriveCommand });
                }
                else // The driver has already arrived (HasArrived == true)
                {
                    // STATUS 2: The driver has arrived. Now we must decide the next action.             

                    // We check if a signature is required.
                    // A signature is only required if it is a 'Pickup' type event AND one has not yet been captured.
                    bool isSignatureRequired = Event.EventType == ScheduleEventType.Pickup && !HasSignature;

                    if (isSignatureRequired)
                    {
                        // CASE A: It is a Pickup and needs a signature.
                        Actions.Add(new EventAction { Text = "Update Contact Phone Number", IconGlyph = "", Command = UpdatePhoneCommand });
                        Actions.Add(new EventAction { Text = "Passenger Signature", IconGlyph = "", Command = GoToSignaturePageCommand });
                        Actions.Add(new EventAction { Text = "Cancel Trip", IconGlyph = "", Command = CancelTripCommand });
                    }
                    else
                    {
                        // CASE B: No signature required. This occurs if:
                        // 1. It is a 'Dropoff' type event (which skips the signature).
                        // 2. It is a 'Pickup' type event that already has a signature saved.
                        // In both cases, the next action is "Perform".
                        Actions.Add(new EventAction { Text = "Perform", IconGlyph = "", Command = PerformCommand });
                        Actions.Add(new EventAction { Text = "Cancel Trip", IconGlyph = "", Command = CancelTripCommand });
                    }
                }

            }
                

            // Common actions (Call, Map, etc.) are always added at the end.
            Actions.Add(new EventAction { Text = "Call Customer", IconGlyph = "", Command = CallCustomerCommand });
            //Actions.Add(new EventAction { Text = "Text Customer", IconGlyph = "", Command = TextCustomerCommand });

            string mapActionText = Event.TripType == "Appointment"
                ? "Maps - Appointment Address"
                : "Maps - Return Address";

            Actions.Add(new EventAction { Text = mapActionText, IconGlyph = "", Command = MapsCommand });
            //Actions.Add(new EventAction { Text = "Maps - Appointment Address", IconGlyph = "", Command = MapsCommand });

            // Disabled, not removed, and in the same place: drivers ask for a call with the Call me
            // button now, so the office calls back with the route in front of it. No command set
            // at all, so the row cannot dial even if the platform lets a tap through a disabled
            // view. CallDispatchCommand stays, for the day this decision is reversed.
            Actions.Add(new EventAction
            {
                Text = "Call Dispatch",
                IconGlyph = "",
                IsEnabled = false,
                Hint = "Use the Call me button instead"
            });
            //Actions.Add(new EventAction { Text = "Send Dispatch Message", IconGlyph = "", Command = SendDispatchMessageCommand });
        }

        [RelayCommand]
        private async Task UpdatePhone()
        {            
            if (Event?.TripId == null || Event.CustomerId == null)
            {
                await Shell.Current.DisplayAlert("Error", "Contact information is not available for this event.", "OK");
                return;
            }
           
            string newPhone = await Shell.Current.DisplayPromptAsync(
                "Update Phone Number",
                "Enter the new contact phone number for the customer:",
                "Save",
                "Cancel",
                placeholder: "e.g., 555-123-4567",
                initialValue: Event.CustomerPhone, 
                keyboard: Keyboard.Telephone);

            // The user canceled or left the field empty.
            if (string.IsNullOrWhiteSpace(newPhone))
            {
                return;
            }

            IsBusy = true;
            try
            {
                bool success = await _scheduleService.UpdateContactPhoneNumberAsync(Event.TripId.Value, newPhone);

                if (success)
                {
                    
                    Event.Phone = newPhone;
                    Event.CustomerPhone = newPhone;
                   
                    OnPropertyChanged(nameof(Event));

                    await Shell.Current.DisplayAlert("Success", "The phone number has been updated.", "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to update the phone number. Please check your connection and try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating phone number: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred while updating the phone number.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToSignaturePage()
        {
            await Shell.Current.GoToAsync(nameof(SignaturePage), new Dictionary<string, object>
        {
            { "ScheduleId", Event.Id }
        });
        }

        [RelayCommand]
        private async Task Arrive()
        {
            if (IsBusy || Event is null) return;

            IsBusy = true;
            try
            {
                // GET CURRENT DEVICE LOCATION
                var currentLocation = await _gpsService.GetCurrentLocationAsync();

                if (currentLocation == null)
                {
                    // If the location could not be obtained, we inform the user and cancel the action.
                    await Shell.Current.DisplayAlert("Location Error", "Could not get current location. Please ensure GPS is enabled and permissions are granted.", "OK");
                    return;
                }
               
                var eventLocation = new Location(Event.ScheduleLatitude, Event.ScheduleLongitude);

                // CALCULATE THE DISTANCE
                var distanceInMiles = Location.CalculateDistance(currentLocation, eventLocation, DistanceUnits.Miles);

                // Distance threshold for warning
                const double ProximityThresholdInMiles = 0.1;

                if (distanceInMiles > ProximityThresholdInMiles)
                {
                    // If the distance is greater than the threshold, we show the alert with two options.
                    bool userConfirmedArrival = await Shell.Current.DisplayAlert(
                        "Proximity Warning", 
                        $"You are {distanceInMiles:F2} miles away from the destination", 
                        "Arrive", // Accept button (returns true)
                        "Cancel"  // Cancel button (returns false)
                    );

                    // If the user pressed "Cancel", we simply exit the method.
                    if (!userConfirmedArrival)
                    {
                        return; // The action is cancelled.
                    }

                    // If the user pressed "Arrive" on the alert, we go ahead and finish the arrival.
                    await FinalizeArrival(distanceInMiles, currentLocation.ToString());
                }
                else
                {
                    // If the distance is less than or equal to the threshold, no warning is displayed.
                    // We proceed directly to finalize the arrival.
                    await FinalizeArrival(distanceInMiles, currentLocation.ToString());
                }
              
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Perform()
        {
            if (IsBusy || Event is null) return;

            IsBusy = true;
            try
            {
                
                var currentLocation = await _gpsService.GetCurrentLocationAsync();

                if (currentLocation == null)
                {
                    await Shell.Current.DisplayAlert("Location Error", "Could not get current location. Please ensure GPS is enabled and permissions are granted.", "OK");
                    return;
                }

                var eventLocation = new Location(Event.ScheduleLatitude, Event.ScheduleLongitude);
                var distanceInMiles = Location.CalculateDistance(currentLocation, eventLocation, DistanceUnits.Miles);

                
                const double ProximityThresholdInMiles = 0.1;

                if (distanceInMiles > ProximityThresholdInMiles)
                {
                    // If the distance is greater, we show the alert with the "Perform" and "Cancel" buttons.
                    bool userConfirmedPerform = await Shell.Current.DisplayAlert(
                        "Proximity Warning",
                        $"You are {distanceInMiles:F2} miles away from the destination.",
                        "Perform", // Accept button
                        "Cancel"   // Cancel button
                    );

                    if (!userConfirmedPerform)
                    {
                        return; // If the user presses "Cancel", we exit.
                    }

                    // If the user confirms, we proceed to finish.
                    await FinalizePerform(distanceInMiles);
                }
                else
                {
                    // If the distance is acceptable, we proceed directly.
                    await FinalizePerform(distanceInMiles);
                }
            }
            finally
            {
                // We make sure that the busy indicator is always disabled.
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelTrip()
        {
            if (IsBusy || Event?.TripId is null) return;

            // --- Show the first options menu (No Show / Cancel At Door) ---
            var firstChoice = await Shell.Current.DisplayActionSheet(
                "Cancel Trip", // Title
                "Cancel",      // Cancel button
                null,          // Destroy button (none)
                "No Show", "Cancel At Door");

            if (firstChoice == "No Show")
            {
                // The user selected "No Show", we proceed to show the reasons.
                await HandleNoShowCancellation();
            }
            else if (firstChoice == "Cancel At Door")
            {
                
                await Shell.Current.DisplayAlert("Not Implemented", "Cancel At Door functionality will be implemented later.", "OK");
            }
            // If the user presses "Cancel" or anything else, nothing is done.
        }
        [RelayCommand]
        private async Task CallCustomer()
        {                   
            if (Event == null || string.IsNullOrWhiteSpace(Event.CustomerPhone))
            {
                await Shell.Current.DisplayAlert("Not available", "There is no contact phone number for this event.", "OK");
                return;
            }

            try
            {
                // Ask the user if they want to make the call.
                bool wantsToCall = await Shell.Current.DisplayAlert(
                    "Confirm call", //"Confirmar llamada",
                    $"Do you want to call this number?\n{Event.CustomerPhone}", // $"¿Deseas llamar a este número?\n{Event.CustomerPhone}",
                    "Call", // "Llamar",
                    "Cancel"); // "Cancelar");

                if (wantsToCall)
                {
                    // Check if the device supports making calls.
                    if (_phoneDialer.IsSupported)
                    {
                        // Open phone dialer with number.
                        _phoneDialer.Open(Event.CustomerPhone);
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Not supported", "This device cannot make calls.", "OK");
                    }
                }
            }
            // Handle possible exceptions
            catch (FeatureNotSupportedException)
            {
                await Shell.Current.DisplayAlert("Not supported", "Calling functionality is not supported on this device.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when trying to call: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred while trying to make the call.", "OK");
            }
        }
        [RelayCommand] private void TextCustomer() => Debug.WriteLine("Text Customer Tapped");

        [RelayCommand]
        private async Task Maps()
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

        [RelayCommand] private void SendDispatchMessage() => Debug.WriteLine("Send Dispatch Tapped");
        
        [RelayCommand]
        private async Task CallDispatch()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                
                var provider = await _providerService.GetContactProviderAsync();

                if (provider == null || string.IsNullOrWhiteSpace(provider.Phone))
                {
                    await Shell.Current.DisplayAlert("Not available", "The office phone number is not configured.", "OK");
                    return;
                }
             
                if (await Shell.Current.DisplayAlert("Confirm call", $"Do you want to call the office?\n{provider.Phone}", "Call", "Cancel"))
                {
                    
                    if (_phoneDialer.IsSupported)
                    {
                        _phoneDialer.Open(provider.Phone);
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Not supported", "This device cannot make calls.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when trying to call the office: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred while trying to make the call.", "OK");
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
        private async Task CopyPhone()
        {
            // Copied the address, not the phone. The alert said "phone number", so the driver
            // pasted a street into a dialler and had no reason to suspect it.
            var phone = !string.IsNullOrWhiteSpace(Event?.CustomerPhone)
                ? Event.CustomerPhone
                : Event?.Phone;

            if (!string.IsNullOrWhiteSpace(phone))
            {
                await Clipboard.SetTextAsync(phone);
                await Shell.Current.DisplayAlert("Copied", "The phone number has been copied to the clipboard.", "OK");
            }
        }

        private async Task FinalizeArrival(double distanceInMiles, string gpsArrive)
        {
            TimeSpan? oldEta = Event.ETA;

            Event.Arrive = DateTime.Now.TimeOfDay;
            Event.ArriveDist = distanceInMiles;
            Event.GPSArrive = gpsArrive;
            Event.ETA = Event.Arrive;
           
            var success = await _scheduleService.UpdateScheduleAsync(Event);

            if (success)
            {               
                HasArrived = true;
                BuildActionsList(); // Update the UI
                if (!_gpsService.IsTracking)
                    _gpsService.StartTracking(Event.VehicleRouteId);

                // If the event type is Pickup, we send the notification to the passenger.
                if (Event.EventType == ScheduleEventType.Pickup)
                    await SendNotificationToMember();

            }
            else
            {
                // Roll back local changes if the API fails to maintain consistency
                Event.Arrive = null;
                Event.ArriveDist = null;
                Event.GPSArrive = null;
                Event.ETA = oldEta;
                await Shell.Current.DisplayAlert("Error", "Could not save arrival time. Please try again.", "OK");
            }
        }

        // Enviar notificación al miembro a través del servicio de Zonitel
        private async Task SendNotificationToMember() {
            
            HttpClient client = new HttpClient();
            ApiZonitelService _apiZonitelService = new ApiZonitelService(client);

            // Limpiar el teléfono (Quitar espacios, guiones y el prefijo +1 si existe)
            string cleanPhone = Event.Phone.Trim();

            // Si empieza con +1, quitamos los primeros 2 caracteres
            if (cleanPhone.StartsWith("+1"))
            {
                cleanPhone = cleanPhone.Substring(2);
            }
            // Si por casualidad empieza con 1 (sin el +), quitamos el primer caracter
            else if (cleanPhone.StartsWith("1") && cleanPhone.Length > 10)
            {
                cleanPhone = cleanPhone.Substring(1);
            }

            // Limpieza adicional por si el número viene con formato (786) 483-6314
            cleanPhone = new string(cleanPhone.Where(char.IsDigit).ToArray());

            try
            {
                bool isSent = await _apiZonitelService.SendSMSMessageDriverArrivesAtThePickupLocation(cleanPhone);
                if (isSent)
                {
                    await Shell.Current.DisplayAlert("Success", "Notification sent successfully to the passenger.", "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "The SMS could not be sent. Please check the API logs or connection.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"An error occurred while sending the SMS: {ex.Message}", "OK");
                throw;
            }
        }

        private async Task FinalizePerform(double distanceInMiles)
        {
            //var pendingEvents = await _scheduleService.GetPendingSchedulesByRunAsync(RunLogin, Event.Date.Value.Date);

            TimeSpan? oldEta = Event.ETA;

            Event.Perform = DateTime.Now.TimeOfDay; 
            Event.PerformDist = distanceInMiles;    
            Event.Performed = true;
            Event.ETA = Event.Perform;

            var success = await _scheduleService.PerformScheduleUpdateAsync(Event);
            //var success = await _scheduleService.UpdateScheduleAsync(Event);

            if (success)
            {
                var pendingEvents = Events.ToList();
                await _scheduleService.UpdateNextSchedulesETAsAsync(pendingEvents, Event.Id, Event.Perform.Value);

                if (!_gpsService.IsTracking)
                    _gpsService.StartTracking(Event.VehicleRouteId);
                // ".." is the shell syntax to go to the previous page (TodaySchedulePage)
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                // Roll back local changes if the API fails to maintain consistency
                Event.Perform = null;
                Event.PerformDist = null;
                Event.Performed = false;
                Event.ETA = oldEta;
                await Shell.Current.DisplayAlert("Error", "Could not save perform data. Please try again.", "OK");
            }
        }

        private async Task HandleNoShowCancellation()
        {
            // Show the second menu with the reasons for "No Show" ---
            var reason = await Shell.Current.DisplayActionSheet(
                "Reason for Cancellation", // Title
                "Cancel",                  // Cancel button
                null,                      // Destruction button
                "TIENE OTRA TRANSPORTACION",
                "NO CONTESTA EL TELEFONO NI LA PUERTA",
                "NO HA SALIDO Y LLEVO MAS DE 15 MINUTOS ESPERANDO",
                "NO QUIERE IR");

            // We check if the user selected a valid reason (it was not "Cancel" or closed the dialog)
            if (reason != null && reason != "Cancel")
            {
                IsBusy = true;
                try
                {
                    
                    bool success = await _scheduleService.CancelTripByDriverAsync(Event.TripId.Value, reason);

                    if (success)
                    {
                        await Shell.Current.DisplayAlert("Success", "The trip has been cancelled.", "OK");
                        // Navigate back to event list
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Error", "Could not cancel the trip. Please try again.", "OK");
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}