namespace Raphael.Driver.Views
{
    public partial class ContactPage : ContentPage
    {
        public ContactPage(ViewModels.ContactViewModel viewModel)
        {
            InitializeComponent();
            // RE-064: the menu at the bottom, in the style chosen in Settings.
            Controls.Menu.NavMenuView.Attach(this, "contact");
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ViewModels.ContactViewModel vm)
            {

                vm.InitializeCommand.Execute(null);
            }
        }
    }
}