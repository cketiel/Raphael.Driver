using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views
{
    public partial class VehicleInspectionPage : ContentPage
    {
        public VehicleInspectionPage(VehicleInspectionViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
