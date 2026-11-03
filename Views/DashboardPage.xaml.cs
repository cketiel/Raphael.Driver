using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class DashboardPage : ContentPage
{
	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		// RE-064: the menu at the bottom, in the style chosen in Settings.
		Controls.Menu.NavMenuView.Attach(this, "home");
        BindingContext = viewModel;
    }
  
}