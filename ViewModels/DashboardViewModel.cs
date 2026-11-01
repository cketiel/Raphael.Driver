
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Raphael.Driver.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IRunService _runService;
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _driverName;

        [ObservableProperty]
        private string _runName;

        [ObservableProperty]
        private string _vehicleName;

        [ObservableProperty]
        private ScheduleDto _nextEvent;

        [ObservableProperty]
        private int _remainingEventsCount;

        [ObservableProperty]
        private string _runLogin; 

        [ObservableProperty]
        private bool _hasData; // To show/hide views

        // V7 "Today's Events" card: one bar segment per event of the day, the next one flagged.
        public ObservableCollection<EventSegment> Segments { get; } = new();

        [ObservableProperty]
        private string _nextEventCaption = string.Empty;

        [ObservableProperty]
        private string _nextEventTitle = string.Empty;

        [ObservableProperty]
        private string _nextEventGlyph = string.Empty;

        [ObservableProperty]
        private bool _hasNextEvent;

        [ObservableProperty]
        private bool _allDone;

        // The pending list exactly as Today loads it: the detail pages need it to move the ETAs on.
        private ObservableCollection<ScheduleDto> _pendingEvents = new();

        public DashboardViewModel(IRunService runService, IScheduleService scheduleService)
        {
            _runService = runService;
            _scheduleService = scheduleService;

        }


        [RelayCommand]
        private async Task InitializeAsync()
        {
            IsBusy = true;
            HasData = false;
            try
            {
                //var userId = Preferences.Get("UserId", 0);
                var userIdStr = Preferences.Get("UserId", "0");
                if (!int.TryParse(userIdStr, out int userId) || userId == 0)
                {
                    // If it cannot be converted or is 0, we do not continue.
                    return;
                }
                //userId = 6; // para probar
                if (userId == 0) return;

                // Get the active route
                var activeRun = await _runService.GetActiveRunByDriverIdAsync(userId);

                if (activeRun != null)
                {                    
                    RunLogin = activeRun.SmartphoneLogin;
                   
                    var pendingEvents = await _scheduleService.GetPendingSchedulesByRunAsync(activeRun.SmartphoneLogin, DateTime.Today);
                   
                    DriverName = activeRun.Driver?.FullName ?? "N/A"; 
                    RunName = activeRun.Name;
                    VehicleName = activeRun.Vehicle?.Name ?? "N/A"; 

                    NextEvent = pendingEvents.FirstOrDefault();
                    RemainingEventsCount = pendingEvents.Count();
                    _pendingEvents = new ObservableCollection<ScheduleDto>(pendingEvents);

                    // RE-057: the same resend Today does on load. A driver who works the day from
                    // here may never open Today, and a start made without signal must still reach
                    // the server.
                    var serverStatuses = pendingEvents
                        .Where(ev => ev.TripId.HasValue)
                        .GroupBy(ev => ev.TripId!.Value)
                        .ToDictionary(g => g.Key, g => g.First().Status);
                    _ = Helpers.StartedTrips.RetryPendingAsync(_scheduleService, serverStatuses);

                    var doneCount = await _scheduleService.GetScheduleHistoryCountAsync(activeRun.SmartphoneLogin, DateTime.Today);
                    ShowProgress(doneCount, RemainingEventsCount);
                    ShowNextEvent(NextEvent);
                    HasData = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowProgress(int done, int pending)
        {
            Segments.Clear();

            for (var i = 0; i < done; i++)
                Segments.Add(new EventSegment(IsDone: true, IsNext: false));

            for (var i = 0; i < pending; i++)
                Segments.Add(new EventSegment(IsDone: false, IsNext: i == 0));
        }

        private void ShowNextEvent(ScheduleDto? next)
        {
            HasNextEvent = next is not null;
            AllDone = next is null;

            if (next is null)
                return;

            var isGarage = next.Name is "Pull-out" or "Pull-in";
            var time = next.ETA is { } eta ? DateTime.Today.Add(eta).ToString("hh:mm tt") : string.Empty;

            NextEventCaption = string.IsNullOrEmpty(time) ? "NEXT EVENT" : $"NEXT EVENT · {time}";
            NextEventTitle = isGarage
                ? (next.Name == "Pull-out" ? "Pull Out" : "Pull In")
                : $"{next.EventType} · {next.Patient}";

            // fa-car for the garage, fa-user for a pickup, fa-location-dot for a drop-off.
            NextEventGlyph = isGarage ? "" : next.EventType == Models.ScheduleEventType.Pickup ? "" : "";
        }

        /// <summary>
        /// Opens the next event straight from the Dashboard. It is the first pending event, the
        /// same one Today opens with its actions enabled, and it goes with the same three values
        /// Today sends, so the order of the day is enforced exactly as it is there.
        /// </summary>
        [RelayCommand]
        private async Task OpenNextEvent()
        {
            if (IsBusy || NextEvent is null) return;

            var page = NextEvent.Name is "Pull-out" or "Pull-in" ? nameof(PullOutDetailPage) : nameof(EventDetailPage);

            await Shell.Current.GoToAsync(page, new Dictionary<string, object>
            {
                { "EventDetail", NextEvent },
                { "IsFirstEvent", true },
                { "Events", _pendingEvents }
            });
        }

        [RelayCommand]
        private async Task GoToSchedule()
        {
            if (IsBusy) return;

            // No run of this driver's today: the Schedule page, where a run login can be typed.
            if (string.IsNullOrWhiteSpace(RunLogin))
            {
                await Shell.Current.GoToAsync("//SchedulePage");
                return;
            }

            IsBusy = true;
            try
            {
                await Shell.Current.GoToAsync(nameof(TodaySchedulePage), new Dictionary<string, object>
                {
                    { "runLogin", RunLogin }
                });
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
namespace Raphael.Driver.ViewModels
{
    /// <summary>One segment of the Dashboard's progress bar: an event of the day.</summary>
    public record EventSegment(bool IsDone, bool IsNext);
}
