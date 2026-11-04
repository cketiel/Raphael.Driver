using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Views; 
using System.Diagnostics;
using System.Web; 

namespace Raphael.Driver.ViewModels
{
    public partial class ScheduleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _runLogin;

        //[ObservableProperty]
        //private string _vehicleLogin;

        [ObservableProperty]
        private bool _isBusy;

        public ScheduleViewModel() { }

        [RelayCommand]
        private async Task GoToTodaySchedule()
        {
            if (IsBusy) return; // Avoid multiple clicks

            if (string.IsNullOrWhiteSpace(RunLogin))
            {
                await Shell.Current.DisplayAlert("Input Required", "Please enter a Run Login.", "OK");
                return;
            }

            try
            {
                IsBusy = true; 

                var navigationParameter = new Dictionary<string, object>
                {
                    { "runLogin", RunLogin }
                };

                await Shell.Current.GoToAsync("//" + nameof(TodaySchedulePage), navigationParameter);
            }
            catch (Exception ex)
            {               
                Debug.WriteLine($"Error while browsing: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", "The schedule page could not be opened.", "OK");
            }
            finally
            {
                IsBusy = false; 
            }      
        }

        [RelayCommand]
        private async Task GoToFutureSchedule()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(RunLogin))
            {
                await Shell.Current.DisplayAlert("Input Required", "Please enter a Run Login.", "OK");
                return;
            }

            try
            {
                IsBusy = true;
                await Shell.Current.GoToAsync("//" + nameof(FutureSchedulePage), new Dictionary<string, object>
                {
                    { "runLogin", RunLogin }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to future schedule: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}