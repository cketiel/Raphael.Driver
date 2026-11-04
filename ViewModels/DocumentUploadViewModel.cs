using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics.Platform;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;

namespace Raphael.Driver.ViewModels
{
    /// <summary>One file of the document: its one page, or one of the five photos.</summary>
    public partial class DocumentSlot : ObservableObject
    {
        public required string Slot { get; init; }
        public string Title => DocumentsViewModel.SlotName(Slot);

        /// <summary>What to frame, in a sentence.</summary>
        public string Hint => Slot switch
        {
            "FRONT" => "Stand in front of the vehicle and fit the whole front in the photo.",
            "BACK" => "From behind, with both rear doors and the plate in the photo.",
            "RIGHT" => "The passenger side, from the front wheel to the rear one.",
            "LEFT" => "The driver side, from the front wheel to the rear one.",
            "INTERIOR" => "From the rear doors, showing the seats and the wheelchair space.",
            _ => "A clear photo of the whole page, or the PDF you received."
        };

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasFile), nameof(IsImage), nameof(IsPdf))]
        private byte[]? _content;

        [ObservableProperty] private string _contentType = "image/jpeg";
        [ObservableProperty] private ImageSource? _preview;
        [ObservableProperty] private bool _isCurrent;

        public bool HasFile => Content is { Length: > 0 };
        public bool IsImage => HasFile && ContentType != "application/pdf";
        public bool IsPdf => HasFile && ContentType == "application/pdf";
    }

    /// <summary>
    /// Uploading or renewing one document (RE-065). A one-page document takes a photo, an image
    /// from the gallery or a PDF. The vehicle photos are guided: five steps, each with the outline
    /// of the side to frame, then a summary to check before sending.
    /// </summary>
    public partial class DocumentUploadViewModel : ObservableObject, IQueryAttributable
    {
        private const int MaxBytes = 10 * 1024 * 1024;
        private readonly IComplianceService _compliance;
        private ComplianceRow? _row;

        public ObservableCollection<DocumentSlot> Slots { get; } = new();

        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string? _ownerLine;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSummary), nameof(StepText), nameof(CanGoBack), nameof(Current), nameof(NextText))]
        private bool _isGuided;

        /// <summary>The step of the guided photos; equal to the number of slots on the summary.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Current), nameof(IsSummary), nameof(StepText), nameof(CanGoBack), nameof(NextText))]
        private int _step;

        [ObservableProperty] private DateTime _expiresOn = DateTime.Today;
        [ObservableProperty] private bool _hasPickedDate;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _error;

        public DateTime MinimumDate => DateTime.Today;

        public DocumentSlot? Current => Step < Slots.Count ? Slots[Step] : null;
        public bool IsSummary => !IsGuided || Step >= Slots.Count;
        public string StepText => IsGuided && Step < Slots.Count ? $"Photo {Step + 1} of {Slots.Count} · {Current!.Title}" : string.Empty;
        public bool CanGoBack => IsGuided && Step > 0;
        public string NextText => Current?.HasFile == true ? "Next" : "Skip for now";

        public DocumentUploadViewModel(IComplianceService compliance)
        {
            _compliance = compliance;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Row", out var value) && value is ComplianceRow row) Prepare(row);
        }

        private void Prepare(ComplianceRow row)
        {
            _row = row;
            Name = row.Name;
            OwnerLine = row.Owner.SubjectName;
            Slots.Clear();
            foreach (var slot in row.Item.Type.Slots) Slots.Add(new DocumentSlot { Slot = slot });
            IsGuided = Slots.Count > 1;
            Step = 0;
            // Today until the driver picks one: a date chosen by nobody is never sent.
            ExpiresOn = DateTime.Today;
            HasPickedDate = false;
            Error = null;
            MarkCurrent();
        }

        partial void OnStepChanged(int value) => MarkCurrent();

        partial void OnExpiresOnChanged(DateTime value) => HasPickedDate = true;

        private void MarkCurrent()
        {
            for (var i = 0; i < Slots.Count; i++) Slots[i].IsCurrent = i == Step;
        }

        [RelayCommand]
        private async Task TakePhotoAsync(DocumentSlot? slot) => await SetAsync(slot ?? Current ?? Slots.FirstOrDefault(), await CaptureAsync(fromGallery: false));

        [RelayCommand]
        private async Task PickImageAsync(DocumentSlot? slot) => await SetAsync(slot ?? Current ?? Slots.FirstOrDefault(), await CaptureAsync(fromGallery: true));

        [RelayCommand]
        private async Task PickPdfAsync(DocumentSlot? slot)
        {
            slot ??= Slots.FirstOrDefault();
            if (slot is null) return;
            try
            {
                var file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose the PDF",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        [DevicePlatform.Android] = new[] { "application/pdf" },
                        [DevicePlatform.iOS] = new[] { "com.adobe.pdf" },
                        [DevicePlatform.MacCatalyst] = new[] { "com.adobe.pdf" }
                    })
                });
                if (file is null) return;
                await using var stream = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                await SetAsync(slot, (buffer.ToArray(), "application/pdf"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PDF picker: {ex.GetType().Name}");
                await Shell.Current.DisplayAlert("PDF", "The file could not be opened. Try again.", "OK");
            }
        }

        [RelayCommand]
        private void Next()
        {
            if (Step < Slots.Count) Step++;
        }

        [RelayCommand]
        private void Back()
        {
            if (Step > 0) Step--;
        }

        /// <summary>From the summary: back to one photo to take it again.</summary>
        [RelayCommand]
        private void Retake(DocumentSlot slot) => Step = Slots.IndexOf(slot);

        /// <summary>What the overlay says while the files go up.</summary>
        [ObservableProperty] private string _uploadStatus = "Uploading…";

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (_row is null || IsBusy) return;
            Error = null;

            var missing = Slots.Where(s => !s.HasFile).ToList();
            if (missing.Count > 0)
            {
                Error = IsGuided
                    ? $"Missing: {string.Join(", ", missing.Select(m => m.Title))}. All five photos are needed."
                    : "Add the photo or the PDF of the document.";
                return;
            }
            if (!HasPickedDate)
            {
                Error = "Choose the expiration date printed on the document.";
                return;
            }

            IsBusy = true;
            var total = Slots.Count;
            UploadStatus = total > 1 ? $"Uploading photo 1 of {total}…" : "Uploading…";
            var progress = new Progress<int>(sent => UploadStatus = total > 1 ? $"Uploading photo {sent + 1} of {total}…" : "Uploading…");
            try
            {
                var result = await _compliance.UploadAsync(new ComplianceDocumentCreateDto
                {
                    DocumentTypeId = _row.Item.Type.Id,
                    Subject = _row.Owner.Subject,
                    SubjectId = _row.Owner.SubjectId,
                    // A date with no time: midnight, unspecified, as the server reads it.
                    ExpiresOn = DateTime.SpecifyKind(ExpiresOn.Date, DateTimeKind.Unspecified)
                }, Slots.Select(s => (s.Slot, s.Content!, s.ContentType)).ToList(), progress);

                if (!result.Ok)
                {
                    Error = result.Error;
                    return;
                }

                await Shell.Current.DisplayAlert(Name, "Sent. It counts once an admin approves it; you will be notified.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SetAsync(DocumentSlot? slot, (byte[] Content, string ContentType)? file)
        {
            if (slot is null || file is null) return;
            if (file.Value.Content.Length > MaxBytes)
            {
                await Shell.Current.DisplayAlert(Name, "The file is larger than 10 MB. Use a smaller one.", "OK");
                return;
            }
            slot.ContentType = file.Value.ContentType;
            slot.Content = file.Value.Content;
            var bytes = file.Value.Content;
            slot.Preview = slot.IsImage ? ImageSource.FromStream(() => new MemoryStream(bytes)) : null;
            OnPropertyChanged(nameof(NextText));
            Error = null;

            // A photo taken in the guided steps moves on to the next one by itself.
            if (IsGuided && slot == Current) Step++;
        }

        /// <summary>The camera or the gallery, as a JPEG at most 1600 px on its long side. Null when cancelled.</summary>
        private static async Task<(byte[], string)?> CaptureAsync(bool fromGallery)
        {
            try
            {
                FileResult? file;
                if (fromGallery)
                {
                    file = await MediaPicker.Default.PickPhotoAsync();
                }
                else
                {
                    if (!MediaPicker.Default.IsCaptureSupported)
                    {
                        await Shell.Current.DisplayAlert("Camera", "This phone cannot take photos from the app.", "OK");
                        return null;
                    }
                    if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
                    {
                        await Shell.Current.DisplayAlert("Camera", "The camera permission is needed to take the photo.", "OK");
                        return null;
                    }
                    file = await MediaPicker.Default.CapturePhotoAsync();
                }
                if (file is null) return null;

                await using var stream = await file.OpenReadAsync();
                var image = PlatformImage.FromStream(stream);
                var resized = image.Width > 1600 || image.Height > 1600 ? image.Downsize(1600, disposeOriginal: true) : image;
                using var output = new MemoryStream();
                await resized.SaveAsync(output, ImageFormat.Jpeg, 0.8f);
                return (output.ToArray(), "image/jpeg");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RAPHAEL-PHOTO: {ex.GetType().Name}");
                await Shell.Current.DisplayAlert("Photo", "The photo could not be taken. Try again.", "OK");
                return null;
            }
        }
    }
}
