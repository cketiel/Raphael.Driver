namespace Raphael.Driver.Views
{
    /// <summary>Settings › MY DOCUMENTS (RE-065).</summary>
    public partial class DocumentsPage : ContentPage
    {
        public DocumentsPage(ViewModels.DocumentsViewModel viewModel)
        {
            InitializeComponent();
            Controls.Menu.NavMenuView.Attach(this, "settings"); // reached from Settings
            BindingContext = viewModel;
        }

        // The first time, and after an upload: not on every return to the page.
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ViewModels.DocumentsViewModel vm) await vm.LoadIfNeededAsync();
        }
    }
}
