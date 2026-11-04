using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using System.Collections.ObjectModel;

namespace Raphael.Driver.ViewModels
{   
    [QueryProperty(nameof(RunLogin), "runLogin")]
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private string _runLogin;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _historyDateText;

        public ObservableCollection<ScheduleHistoryDto> Events { get; } = new();

        public HistoryViewModel(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;           
            HistoryDateText = $"Showing Today's History: {DateTime.Today:MM/dd/yyyy}";
        }
       
        async partial void OnRunLoginChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                var completedEvents = await _scheduleService.GetScheduleHistoryAsync(RunLogin, DateTime.Today);
                Events.Clear();
                foreach (var ev in completedEvents)
                {
                    Events.Add(ev);
                }
            }
            finally
            {
                IsBusy = false;
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
            await Shell.Current.GoToAsync("//TodaySchedulePage");
        }
    }
}