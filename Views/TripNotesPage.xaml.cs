using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views
{
    /// <summary>The notes of one stop (RE-054). All the logic is in <see cref="TripNotesViewModel"/>.</summary>
    public partial class TripNotesPage : ContentPage
    {
        public TripNotesPage(TripNotesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
