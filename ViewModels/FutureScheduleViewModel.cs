using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Raphael.Driver.ViewModels
{
    /// <summary>
    /// Tomorrow's schedule for this run. Tomorrow only, whatever the button is called.
    /// </summary>
    /// <remarks>
    /// It used to list every day ahead. A driver with a week planned got one list holding
    /// several Pull-outs and several Pull-ins, with nothing to tell them which day a row
    /// belonged to — and the only thing a driver acts on before finishing a shift is the next
    /// day's work.
    /// </remarks>
    [QueryProperty(nameof(RunLogin), "runLogin")]
    public partial class FutureScheduleViewModel : ObservableObject
    {
        private readonly IScheduleService _scheduleService;

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

        public FutureScheduleViewModel(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
            Events = new ObservableCollection<ScheduleDto>();
        }

        private void UpdateUiState()
        {
            HasEvents = Events.Any();
            ShowNoEventsMessage = !HasEvents && !IsBusy;
        }

        [RelayCommand]
        private async Task LoadEventsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                UpdateUiState();
                
                var nextDayEvents = await _scheduleService.GetNextDaySchedulesByRunAsync(RunLogin);

                Events.Clear();
                foreach (var ev in nextDayEvents)
                {
                    Events.Add(ev);
                }
            }
            catch (RouteNotAllowedException)
            {
                Events.Clear();
                await Shell.Current.DisplayAlert(RouteNotAllowedException.Title, RouteNotAllowedException.Explanation, "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading next day events: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "Could not load tomorrow's schedule.", "OK");
            }
            finally
            {
                IsBusy = false;
                UpdateUiState();
            }
        }

        [RelayCommand]
        private async Task SelectEvent(ScheduleDto selectedEvent)
        {
            if (selectedEvent == null) return;
           
            await Shell.Current.GoToAsync(nameof(FutureDetailPage), new Dictionary<string, object>
            {
                { "EventDetail", selectedEvent }
            });
        }

        partial void OnRunLoginChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _ = LoadEventsAsync();
            }
        }
    }
}