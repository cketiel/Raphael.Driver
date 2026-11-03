using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views;

public partial class HistoryPage : ContentPage
{
	public HistoryPage(HistoryViewModel viewModel)
	{
		InitializeComponent();
        // RE-064: the menu at the bottom; these pages are part of Schedule.
        Controls.Menu.NavMenuView.Attach(this, "schedule");
        BindingContext = viewModel;
    }
}