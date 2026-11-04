using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Raphael.Driver.ViewModels
{
    [QueryProperty(nameof(RunLogin), "runLogin")]
    public partial class TodayScheduleViewModel : ObservableObject
    {
        private readonly IScheduleService _scheduleService;
        private readonly ISessionManagerService _sessionManager;
        private readonly Dictionary<ScheduleDto, string> _drawn = new(ReferenceEqualityComparer.Instance);

        [ObservableProperty]
        private bool _isHistoryAvailable;

        [ObservableProperty]
        private ObservableCollection<ScheduleDto> _events;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _runLogin;

        [ObservableProperty]
        private bool _hasEvents;

        [ObservableProperty]
        private bool _showNoEventsMessage;

        /// <summary>
        /// The route these trips belong to, for the Call me button. Kept when the list empties: the
        /// run is still the same one after its last trip.
        /// </summary>
        [ObservableProperty]
        private int _vehicleRouteId;

        public TodayScheduleViewModel(IScheduleService scheduleService, ISessionManagerService sessionManager)
        {
            _scheduleService = scheduleService;
            _sessionManager = sessionManager;
            Events = new ObservableCollection<ScheduleDto>();
        }
        private void UpdateUiState()
        {
            HasEvents = Events.Any();
            ShowNoEventsMessage = !HasEvents && !IsBusy;
        }

        /// <summary>
        /// Brings the list to what the server sent, touching only the rows that differ, so coming
        /// back from a stop does not draw every card again. A row whose object was changed in
        /// memory since it was drawn counts as different.
        /// </summary>
        private void SyncEvents(List<ScheduleDto> fresh)
        {
            try
            {
                var freshJson = fresh.Select(e => System.Text.Json.JsonSerializer.Serialize(e)).ToList();
                var wanted = fresh.Select((e, i) => (e.Id, Json: freshJson[i])).ToHashSet();

                for (var i = Events.Count - 1; i >= 0; i--)
                {
                    var row = Events[i];
                    if (!_drawn.TryGetValue(row, out var drawn)
                        || drawn != System.Text.Json.JsonSerializer.Serialize(row)
                        || !wanted.Contains((row.Id, drawn)))
                    {
                        _drawn.Remove(row);
                        Events.RemoveAt(i);
                    }
                }

                for (var i = 0; i < fresh.Count; i++)
                {
                    if (i < Events.Count && Events[i].Id == fresh[i].Id) continue;
                    if (Events.Skip(i).Any(e => e.Id == fresh[i].Id))
                        throw new InvalidOperationException("The order changed.");
                    Events.Insert(i, fresh[i]);
                    _drawn[fresh[i]] = freshJson[i];
                }
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Schedule redrawn in full: {ex.GetType().Name}");
            }

            _drawn.Clear();
            Events.Clear();
            foreach (var ev in fresh)
                Events.Add(ev);
            foreach (var ev in fresh)
                try { _drawn[ev] = System.Text.Json.JsonSerializer.Serialize(ev); } catch (Exception) { }
        }

        [RelayCommand]
        private async Task LoadEventsAsync()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(RunLogin))
                return;

            try
            {
                IsBusy = true;
                UpdateUiState();

                var pendingEvents = await _scheduleService.GetPendingSchedulesByRunAsync(RunLogin, DateTime.Today);
                // var pendingEvents = allScheduleEvents.Where(ev => !ev.Performed).ToList();

                SyncEvents(pendingEvents.ToList());

                if (Events.FirstOrDefault() is { } first)
                    VehicleRouteId = first.VehicleRouteId;

                // RE-057: a start made without signal, and the app closed before it reached the
                // server, is sent again now. What the server already shows as started is not.
                var serverStatuses = pendingEvents
                    .Where(ev => ev.TripId.HasValue)
                    .GroupBy(ev => ev.TripId!.Value)
                    .ToDictionary(g => g.Key, g => g.First().Status);
                _ = Helpers.StartedTrips.RetryPendingAsync(_scheduleService, serverStatuses);
             
                //SessionManagerService _sessionManager = new SessionManagerService(new GpsService());
                //await _sessionManager.CheckAndResumeGpsTrackingAsync(Events);

                // RE-062: without coverage the day still shows, from the phone's copy; only the
                // history count needs the server.
                try
                {
                    var historyCount = await _scheduleService.GetScheduleHistoryCountAsync(RunLogin, DateTime.Today);
                    IsHistoryAvailable = historyCount > 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"History count unavailable: {ex.Message}");
                }

            }
            catch (RouteNotAllowedException)
            {
                // RE-063: CHANGE ROUTE to another company's route. Back to where it was typed.
                Events.Clear();
                await Shell.Current.DisplayAlert(RouteNotAllowedException.Title, RouteNotAllowedException.Explanation, "OK");
                await Shell.Current.GoToAsync("//SchedulePage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading events: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Could not load the schedule.", "OK");
            }
            finally
            {
                IsBusy = false;
                UpdateUiState();
            }
        }

        [RelayCommand]
        private void OpenMenu()
        {
            Shell.Current.FlyoutIsPresented = true;
        }

        [RelayCommand]
        private async Task SelectEvent(ScheduleDto selectedEvent)
        {
            if (selectedEvent == null)
                return;

            // The first event in the current list is the only "active" one.
            // We compare the selected event with the first element of the collection.
            bool isFirstEvent = Events.IndexOf(selectedEvent) == 0;
            //bool isFirstEvent = Events.FirstOrDefault() == selectedEvent;
            
            if (selectedEvent.Name == "Pull-in" || selectedEvent.Name == "Pull-out")
            {
                //if (selectedEvent.Name == "Pull-in")
                    //isFirstEvent = Events.Count == 1; // Pull-in can only be the first if it's the only event

                await Shell.Current.GoToAsync(nameof(PullOutDetailPage), new Dictionary<string, object>
                {
                    { "EventDetail", selectedEvent },
                    { "IsFirstEvent", isFirstEvent },
                    { "Events", Events }
                });
            }
            else
            {
                
                await Shell.Current.GoToAsync(nameof(EventDetailPage), new Dictionary<string, object>
                {
                    { "EventDetail", selectedEvent },
                    { "IsFirstEvent", isFirstEvent },
                    { "Events", Events }
                });
            }
        }

        [RelayCommand]
        private async Task GoToHistory()
        {
            if (string.IsNullOrWhiteSpace(RunLogin)) return;
          
            await Shell.Current.GoToAsync("//" + nameof(HistoryPage), new Dictionary<string, object>
            {
                { "runLogin", RunLogin }
            });
        }

        [RelayCommand]
        private async Task ChangeRoute()
        {          
            await Shell.Current.GoToAsync("//SchedulePage");
        }

        // Automatic loading when RunLogin is set
        partial void OnRunLoginChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) Services.ActiveRun.RunLogin = value; // RE-064: the menu's Schedule
            // When the RunLogin property receives a value, events are loaded
            if (!string.IsNullOrWhiteSpace(value))
            {
                _ = LoadEventsAsync();
            }
        }
    }
}