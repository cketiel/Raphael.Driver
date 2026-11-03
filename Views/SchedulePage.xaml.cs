using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class SchedulePage : ContentPage
{
	public SchedulePage()
	{
		InitializeComponent();
		// RE-064: the menu at the bottom, in the style chosen in Settings.
		Controls.Menu.NavMenuView.Attach(this, "schedule");
        BindingContext = new ScheduleViewModel();
    }
}