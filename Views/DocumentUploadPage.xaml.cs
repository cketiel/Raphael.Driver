using System.ComponentModel;
using Raphael.Driver.Controls;
using Raphael.Driver.ViewModels;

namespace Raphael.Driver.Views
{
    /// <summary>Uploading a compliance document (RE-065). The outline follows the guided step.</summary>
    public partial class DocumentUploadPage : ContentPage
    {
        private readonly VehicleSilhouetteDrawable _outline = new();
        private readonly DocumentUploadViewModel _viewModel;

        public DocumentUploadPage(DocumentUploadViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            Silhouette.Drawable = _outline;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.PropertyChanged += OnChanged;
            Redraw();
        }

        protected override void OnDisappearing()
        {
            _viewModel.PropertyChanged -= OnChanged;
            base.OnDisappearing();
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DocumentUploadViewModel.Current)) Redraw();
        }

        private void Redraw()
        {
            _outline.View = _viewModel.Current?.Slot ?? "FRONT";
            Silhouette.Invalidate();
        }
    }
}
