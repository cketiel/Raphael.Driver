using Raphael.Driver.Services;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class PullOutDetailPage : ContentPage
{
    public PullOutDetailPage(PullOutDetailPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel; 
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // The rows of the driver's schedule style: the page's own for Legacy, the styled list otherwise.
        var legacy = ScheduleStyleManager.Current == ScheduleStyle.Legacy;
        LegacyActions.IsVisible = legacy;
        StyledActions.IsVisible = !legacy;
        Hero.Build();
        StyledActions.Build();
    }
    protected override bool OnBackButtonPressed()
    {       
        if (BindingContext is PullOutDetailPageViewModel vm)
        {
            // If the GoBack command can be executed...
            if (vm.GoBackCommand.CanExecute(null))
            {               
                vm.GoBackCommand.Execute(null);
            }
        }

        // We return 'true' to indicate that we have handled the event
        // and prevent the system from trying to close the app.
        return true;
    }
}