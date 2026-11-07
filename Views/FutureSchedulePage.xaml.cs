using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class FutureSchedulePage : ContentPage
{
	public FutureSchedulePage(FutureScheduleViewModel viewModel)
	{
		InitializeComponent();
        // RE-064: the menu at the bottom; these pages are part of Schedule.
        Controls.Menu.NavMenuView.Attach(this, "schedule");
        BindingContext = viewModel;
    }

    /// <summary>
    /// Reloads on the way in, so a trip added to tomorrow while the driver was elsewhere is
    /// there when they come back.
    /// </summary>
    /// <remarks>
    /// ⚠️ The type checked here is this page's view model. It read TodayScheduleViewModel, a
    /// type this page never binds to, so the reload never ran: what the driver saw was
    /// whatever had been loaded the first time the page was opened.
    /// </remarks>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FutureScheduleViewModel future && !string.IsNullOrWhiteSpace(Raphael.Driver.Services.ActiveRun.RunLogin) && future.RunLogin != Raphael.Driver.Services.ActiveRun.RunLogin) future.RunLogin = Raphael.Driver.Services.ActiveRun.RunLogin;

        if (BindingContext is FutureScheduleViewModel vm && vm.LoadEventsCommand.CanExecute(null))
        {
            vm.LoadEventsCommand.Execute(null);
        }
    }
}