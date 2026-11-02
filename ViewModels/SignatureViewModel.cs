using CommunityToolkit.Maui.Core;    
using CommunityToolkit.Maui.Views;   
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Raphael.Driver.ViewModels
{
    [QueryProperty(nameof(ScheduleId), "ScheduleId")]
    [QueryProperty(nameof(IsDriverSignature), "IsDriverSignature")]
    [QueryProperty(nameof(InspectionKind), "InspectionKind")]
    public partial class SignatureViewModel : ObservableObject
    {
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private int _scheduleId;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveSignatureCommand))]
        private ObservableCollection<IDrawingLine> _lines = new();

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowLegalText))] // Notify change in visibility
        [NotifyPropertyChangedFor(nameof(ShowSignaturePad))]
        private bool _isDriverSignature;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveSignatureCommand))] 
        [NotifyPropertyChangedFor(nameof(ShowLegalText))] // Notify change in visibility
        [NotifyPropertyChangedFor(nameof(ShowSignaturePad))]
        private bool _hasAcceptedTerms;

        [ObservableProperty]
        private string _legalText = "I hereby certify that all information provided regarding my activities during this shift is true, complete, and correct to the best of my knowledge. I further confirm that I completed the required pre-trip and post-trip vehicle inspection in accordance with company policy.";

        // PROPERTY CALCULATED FOR THE XAML: Only legal sample if it is Driver and has NOT accepted
        public bool ShowLegalText => IsDriverSignature && !HasAcceptedTerms;
        public bool ShowSignaturePad => !IsDriverSignature || HasAcceptedTerms;
        /// <summary>
        /// RE-062: set when the pad signs a vehicle inspection (<c>PullOut</c> or <c>PullIn</c>). The
        /// image then goes back to the inspection, which sends it with the rest, instead of to the stop.
        /// </summary>
        [ObservableProperty]
        private string? _inspectionKind;

        partial void OnInspectionKindChanged(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            LegalText = value == "PullIn"
                ? "I hereby certify that I personally inspected this vehicle on returning it to the garage (post-trip inspection), and that every reading, answer and photo in this inspection is true, complete and correct to the best of my knowledge."
                : "I hereby certify that I personally inspected this vehicle before leaving the garage (pre-trip inspection), and that every reading, answer and photo in this inspection is true, complete and correct to the best of my knowledge.";
        }

        private bool CanSaveSignature => Lines.Any() && !IsBusy && (!IsDriverSignature || HasAcceptedTerms);
        //private bool CanSaveSignature => Lines.Any() && !IsBusy;

        public SignatureViewModel(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
            Lines.CollectionChanged += OnLinesCollectionChanged;
        }
        
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("IsDriverSignature", out var value))
            {
                IsDriverSignature = Convert.ToBoolean(value);
            }

            // If it is not a driver signature, we automatically accept terms
            if (!IsDriverSignature)
            {
                HasAcceptedTerms = true;
            }
        }

        // IT IS EXECUTED WHEN MAUI ASSIGNS THE VALUE BY NAVIGATION
        partial void OnIsDriverSignatureChanged(bool value)
        {
            // If it is NOT a driver's signature (it is patient), we accept default terms
            // so that the signature pad can be seen directly.
            if (!value)
            {
                HasAcceptedTerms = true;
            }
        }

        // This method will be executed every time a line is added or removed
        private void OnLinesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // We manually notify the command that its "CanExecute" state may have changed.
            SaveSignatureCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void AcceptTerms()
        {
            HasAcceptedTerms = true;           
            ForceLandscape();
        }

        [RelayCommand(CanExecute = nameof(CanSaveSignature))]
        private async Task SaveSignature()
        {
            IsBusy = true;
            try
            {
                // Convert the signature to a stream image
                var signatureStream = await DrawingView.GetImageStream(Lines, new Size(600, 300), Colors.White);

                // Convert stream to byte[]
                using var memoryStream = new MemoryStream();
                await signatureStream.CopyToAsync(memoryStream);
                var signatureBytes = memoryStream.ToArray();

                if (!string.IsNullOrEmpty(InspectionKind))
                {
                    await Shell.Current.GoToAsync("..", new Dictionary<string, object> { { "InspectionSignature", signatureBytes } });
                    return;
                }

                // Convert the bytes to Base64 to send it through the API
                var signatureBase64 = Convert.ToBase64String(signatureBytes);

                var success = await _scheduleService.SaveSignatureAsync(ScheduleId, signatureBase64);
              
                if (success)
                {
                    // We return to the previous page, passing a parameter to indicate that it was saved
                    await Shell.Current.GoToAsync("..", new Dictionary<string, object>
                    {
                        { "SignatureSaved", true }
                    });
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Could not save signature.", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ClearSignature()
        {
            Lines.Clear();
        }

        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
      
        public void ForceLandscape()
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity != null)
                activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;

#elif IOS    
            // On iOS, this must be configured at the app level or with more complex code.
            // The simplest way is to allow all orientations in Info.plist
            // and let the UI adapt.
#endif
        }

        public void RestoreOrientation()
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity != null)
                activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
#endif
        }
    }
}