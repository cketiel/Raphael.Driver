using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics.Platform;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using Raphael.Driver.Views;

namespace Raphael.Driver.ViewModels
{
    /// <summary>One line of the checklist on screen: fine, or a problem with its photo.</summary>
    public partial class InspectionLineViewModel : ObservableObject
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;

        /// <summary>The first line of its group, so the group's heading shows above it.</summary>
        public bool StartsGroup { get; init; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOk), nameof(NotIsOk), nameof(IsProblemMarked), nameof(NotIsProblemMarked), nameof(IsAnswered), nameof(NeedsPhoto), nameof(NoPhoto))]
        private bool? _isProblem;

        /// <summary>Marked as a problem: its photo line shows.</summary>
        public bool NeedsPhoto => IsProblem == true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PhotoText), nameof(HasPhoto), nameof(PhotoSource), nameof(NoPhoto))]
        private byte[]? _photo;

        /// <summary>The thumbnail of the evidence, from memory: nothing is written to the gallery.</summary>
        public ImageSource? PhotoSource => HasPhoto ? ImageSource.FromStream(() => new MemoryStream(Photo!)) : null;

        /// <summary>A problem still waiting for its photo.</summary>
        public bool NoPhoto => IsProblem == true && !HasPhoto;

        public bool IsAnswered => IsProblem.HasValue;
        public bool IsOk => IsProblem == false;
        public bool NotIsOk => !IsOk;
        public bool IsProblemMarked => IsProblem == true;
        public bool NotIsProblemMarked => !IsProblemMarked;
        public bool HasPhoto => Photo is { Length: > 0 };
        public string PhotoText => HasPhoto ? "Evidence photo · tap to retake" : "Photo required · tap to take it";

        // The selected and plain looks are both in the page and swapped with IsVisible. Recolouring one
        // view is not enough: on Android a colour-only change inside these cards was not repainted until
        // something else redrew them (seen 2026-11-01: OK tapped, the line stayed grey). Triggers that
        // set DynamicResource colours are out too (RE-061 H2).
    }

    /// <summary>One card of the checklist: a group of lines under its icon, with how many are answered.</summary>
    public partial class InspectionGroupViewModel : ObservableObject
    {
        public string Name { get; init; } = string.Empty;
        public string Icon { get; init; } = "\uf46c";
        public ObservableCollection<InspectionLineViewModel> Lines { get; } = new();

        public int Answered => Lines.Count(l => l.IsAnswered);
        public bool IsComplete => Answered == Lines.Count;
        public bool HasProblem => Lines.Any(l => l.IsProblem == true);
        public string CountText => $"{Answered}/{Lines.Count}";

        public Color ChipBackground => HasProblem ? Theme("DangerSoftColor") : IsComplete ? Theme("OkSoftColor") : Theme("AccentSoftColor");
        public Color ChipText => HasProblem ? Theme("DangerColor") : IsComplete ? Theme("OkColor") : Theme("AccentColor");

        public void Refresh()
        {
            OnPropertyChanged(nameof(Answered));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(HasProblem));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(ChipBackground));
            OnPropertyChanged(nameof(ChipText));
        }

        /// <summary>The icon of a checklist group (Font Awesome solid).</summary>
        public static string IconFor(string group) => group switch
        {
            "Body" => "\uf5e4",            // car-side
            "Lights" => "\uf0eb",          // lightbulb
            "Fluids" => "\uf613",          // oil-can
            "Tires and fuel" => "\uf52f",  // gas-pump
            "Cleanliness" => "\uf5bd",     // spray-can
            "Equipment" => "\uf552",       // toolbox
            _ => "\uf46c"                  // clipboard-check
        };

        private static Color Theme(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c ? c : Colors.Gray;
    }

    /// <summary>
    /// The vehicle inspection at Pull-out and Pull-in (RE-062): odometer, VIN, the checklist with a
    /// photo for every problem, the comment and the driver's signature.
    /// </summary>
    /// <remarks>
    /// Nothing reaches the server until the driver finishes: then the inspection, its photos and the
    /// signature go through the outbox, so a garage with no coverage loses nothing. Photos are taken
    /// with the camera only (never from the gallery) and shrunk to 1600 px before they are kept.
    /// </remarks>
    [QueryProperty(nameof(Requirement), "Requirement")]
    [QueryProperty(nameof(Event), "EventDetail")]
    public partial class VehicleInspectionViewModel : ObservableObject, IQueryAttributable
    {
        private const string VinCode = "vin";
        private readonly IInspectionService _inspections;
        private readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private byte[]? _signature;

        public VehicleInspectionViewModel(IInspectionService inspections)
        {
            _inspections = inspections;
        }

        public ObservableCollection<InspectionLineViewModel> Lines { get; } = new();

        [ObservableProperty]
        private VehicleInspectionRequirementDto? _requirement;

        /// <summary>The same lines, one card per group, in the checklist's order.</summary>
        public ObservableCollection<InspectionGroupViewModel> Groups { get; } = new();

        // ---- Progress: every line, plus odometer, VIN, comment and signature --------------------
        private int Steps => Lines.Count + 4;
        private int StepsDone => Lines.Count(l => l.IsAnswered) + (OdometerDone ? 1 : 0) + (VinDone ? 1 : 0) + (CommentDone ? 1 : 0) + (IsSigned ? 1 : 0);
        public double Progress => Steps == 0 ? 0 : (double)StepsDone / Steps;
        public int ProblemCount => Lines.Count(l => l.IsProblem == true) + (VinConfirmed == false ? 1 : 0);
        public string ProgressText => $"{StepsDone} of {Steps} done";
        public string ProblemText => ProblemCount switch { 0 => "No problems", 1 => "1 problem", var n => $"{n} problems" };
        public bool HasProblems => ProblemCount > 0;
        public string FinishText => StepsDone == Steps ? "FINISH INSPECTION" : $"FINISH INSPECTION · {Steps - StepsDone} LEFT";

        public bool OdometerDone => long.TryParse(OdometerText?.Trim(), out var o) && o >= 0;
        public bool VinDone => VinConfirmed == true || (VinConfirmed == false && (VinReported?.Trim().Length ?? 0) >= 4 && VinPhoto is { Length: > 0 });
        public bool CommentDone => !string.IsNullOrWhiteSpace(Comment);

        public string OdometerStatus => OdometerDone ? "\uf058" : "\uf111";
        public string VinStatus => VinDone ? "\uf058" : "\uf111";
        public string CommentStatus => CommentDone ? "\uf058" : "\uf111";
        public string SignatureStatus => IsSigned ? "\uf058" : "\uf111";
        public Color OdometerStatusColor => StatusColor(OdometerDone);
        public Color VinStatusColor => StatusColor(VinDone);
        public Color CommentStatusColor => StatusColor(CommentDone);
        public Color SignatureStatusColor => StatusColor(IsSigned);
        private static Color StatusColor(bool done) => done ? Theme("OkColor") : Theme("BorderColor");

        public ImageSource? SignatureSource => _signature is { Length: > 0 } ? ImageSource.FromStream(() => new MemoryStream(_signature)) : null;
        public bool HasVinPhoto => VinPhoto is { Length: > 0 };
        public ImageSource? VinPhotoSource => HasVinPhoto ? ImageSource.FromStream(() => new MemoryStream(VinPhoto!)) : null;

        private void RefreshProgress()
        {
            foreach (var name in new[]
                     {
                         nameof(Progress), nameof(ProblemCount), nameof(ProgressText), nameof(ProblemText), nameof(HasProblems), nameof(FinishText),
                         nameof(OdometerDone), nameof(VinDone), nameof(CommentDone), nameof(OdometerStatus), nameof(VinStatus), nameof(CommentStatus),
                         nameof(SignatureStatus), nameof(OdometerStatusColor), nameof(VinStatusColor), nameof(CommentStatusColor), nameof(SignatureStatusColor),
                         nameof(SignatureSource), nameof(HasVinPhoto), nameof(VinPhotoSource)
                     })
                OnPropertyChanged(name);
            foreach (var group in Groups) group.Refresh();
        }

        partial void OnOdometerTextChanged(string? value) => RefreshProgress();
        partial void OnVinConfirmedChanged(bool? value) => RefreshProgress();
        partial void OnVinReportedChanged(string? value) => RefreshProgress();
        partial void OnVinPhotoChanged(byte[]? value) => RefreshProgress();
        partial void OnCommentChanged(string? value) => RefreshProgress();
        partial void OnIsSignedChanged(bool value) => RefreshProgress();

        [ObservableProperty]
        private ScheduleDto? _event;

        [ObservableProperty]
        private string _title = "VEHICLE INSPECTION";

        /// <summary>The small caption over the vehicle in the header.</summary>
        [ObservableProperty]
        private string _kindCaption = "PRE-TRIP INSPECTION";

        [ObservableProperty]
        private string? _vehicleName;

        [ObservableProperty]
        private string? _odometerText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasVinOnFile), nameof(VinOnFileText), nameof(VinHead), nameof(VinTail), nameof(NoVinOnFile), nameof(AskOtherVin))]
        private string? _vinOnFile;

        /// <summary>Null until the driver answers; true when the vehicle carries the VIN on file.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AskOtherVin), nameof(VinConfirmBackground), nameof(VinOtherBackground), nameof(VinConfirmText), nameof(VinOtherText))]
        private bool? _vinConfirmed;

        [ObservableProperty]
        private string? _vinReported;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VinPhotoText))]
        private byte[]? _vinPhoto;

        [ObservableProperty]
        private string? _comment;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SignatureText))]
        private bool _isSigned;

        [ObservableProperty]
        private bool _isBusy;

        public bool HasVinOnFile => !string.IsNullOrWhiteSpace(VinOnFile);
        public bool NoVinOnFile => !HasVinOnFile;
        public string VinOnFileText => HasVinOnFile ? VinOnFile! : "No VIN on file for this vehicle";

        // The last 4 characters are what the driver checks against the plate, so they are drawn apart.
        private string VinClean => (VinOnFile ?? "").Trim().ToUpperInvariant();
        public string VinHead => VinClean.Length > 4 ? VinClean[..^4] : "";
        public string VinTail => VinClean.Length > 4 ? VinClean[^4..] : VinClean;
        public bool AskOtherVin => VinConfirmed == false || !HasVinOnFile;
        public string VinPhotoText => VinPhoto is { Length: > 0 } ? "VIN photo taken · tap to retake" : "Take a photo of the VIN on the vehicle";
        public string SignatureText => IsSigned ? "Signed · tap to sign again" : "Required · certifies this inspection";

        public Color VinConfirmBackground => VinConfirmed == true ? Theme("OkColor") : Theme("CardBackgroundColor");
        public Color VinOtherBackground => VinConfirmed == false ? Theme("DangerColor") : Theme("CardBackgroundColor");
        public Color VinConfirmText => VinConfirmed == true ? Colors.White : Theme("OkColor");
        public Color VinOtherText => VinConfirmed == false ? Colors.White : Theme("DangerColor");

        private static Color Theme(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c ? c : Colors.Gray;

        partial void OnRequirementChanged(VehicleInspectionRequirementDto? value)
        {
            if (value is null) return;

            Title = value.Kind == "PullIn" ? "PULL-IN INSPECTION" : "PULL-OUT INSPECTION";
            KindCaption = value.Kind == "PullIn" ? "PULL-IN · POST-TRIP INSPECTION" : "PULL-OUT · PRE-TRIP INSPECTION";
            VehicleName = value.VehicleName;
            VinOnFile = value.VinOnFile;
            if (!HasVinOnFile) VinConfirmed = false;

            Lines.Clear();
            Groups.Clear();
            string? group = null;
            foreach (var item in value.Items.Where(i => i.Code != VinCode))
            {
                var line = new InspectionLineViewModel
                {
                    Code = item.Code,
                    Label = item.Label,
                    Group = item.Group,
                    StartsGroup = item.Group != group
                };
                line.PropertyChanged += (_, _) => RefreshProgress();
                Lines.Add(line);

                if (item.Group != group)
                    Groups.Add(new InspectionGroupViewModel { Name = item.Group, Icon = InspectionGroupViewModel.IconFor(item.Group) });
                Groups[^1].Lines.Add(line);
                group = item.Group;
            }
            RefreshProgress();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Requirement", out var r) && r is VehicleInspectionRequirementDto requirement) Requirement = requirement;
            if (query.TryGetValue("EventDetail", out var e) && e is ScheduleDto schedule) Event = schedule;

            // Back from the signature pad with the drawn image.
            if (query.TryGetValue("InspectionSignature", out var png) && png is byte[] bytes && bytes.Length > 0)
            {
                _signature = bytes;
                IsSigned = true;
            }
        }

        [RelayCommand]
        private void ConfirmVin() => VinConfirmed = true;

        [RelayCommand]
        private async Task OtherVin()
        {
            VinConfirmed = false;
            if (VinPhoto is null) await TakeVinPhoto();
        }

        [RelayCommand]
        private async Task TakeVinPhoto()
        {
            var photo = await CaptureAsync();
            if (photo is not null) VinPhoto = photo;
        }

        [RelayCommand]
        private void MarkOk(InspectionLineViewModel line)
        {
            line.IsProblem = false;
            line.Photo = null;
        }

        /// <summary>A problem opens the camera at once: the photo is the evidence.</summary>
        [RelayCommand]
        private async Task MarkProblem(InspectionLineViewModel line)
        {
            line.IsProblem = true;
            var photo = await CaptureAsync();
            if (photo is not null) line.Photo = photo;
        }

        [RelayCommand]
        private async Task RetakePhoto(InspectionLineViewModel line)
        {
            var photo = await CaptureAsync();
            if (photo is not null) line.Photo = photo;
        }

        [RelayCommand]
        private async Task Sign()
        {
            await Shell.Current.GoToAsync(nameof(SignaturePage), new Dictionary<string, object>
            {
                { "IsDriverSignature", true },
                { "InspectionKind", Requirement?.Kind ?? "PullOut" }
            });
        }

        [RelayCommand]
        private async Task Finish()
        {
            if (IsBusy || Requirement is null || Event is null) return;

            var missing = Missing();
            if (missing.Count > 0)
            {
                await Shell.Current.DisplayAlert("Inspection not finished", "Still missing:\n\n• " + string.Join("\n• ", missing), "OK");
                return;
            }

            var odometer = long.Parse(OdometerText!.Trim());

            IsBusy = true;
            try
            {
                double? latitude = null, longitude = null;
                try
                {
                    var location = await Geolocation.Default.GetLastKnownLocationAsync();
                    latitude = location?.Latitude;
                    longitude = location?.Longitude;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Inspection without a position: {ex.Message}");
                }

                var dto = new VehicleInspectionCreateDto
                {
                    ClientId = Guid.NewGuid(),
                    ScheduleId = Event.Id,
                    ChecklistVersion = Requirement.ChecklistVersion,
                    Odometer = odometer,
                    VinConfirmed = VinConfirmed == true,
                    VinReported = VinConfirmed == true ? null : VinReported?.Trim(),
                    Comment = Comment!.Trim(),
                    StartedAtUtc = _startedAtUtc,
                    CompletedAtUtc = DateTime.UtcNow,
                    Latitude = latitude,
                    Longitude = longitude,
                    Items = Lines.Select(l => new VehicleInspectionAnswerDto { Code = l.Code, IsProblem = l.IsProblem == true }).ToList()
                };

                var photos = Lines.Where(l => l.IsProblem == true && l.HasPhoto).ToDictionary(l => l.Code, l => l.Photo!);
                if (VinConfirmed != true && VinPhoto is { Length: > 0 }) photos[VinCode] = VinPhoto;

                var result = await _inspections.SubmitAsync(new FinishedInspection { Inspection = dto, Photos = photos, Signature = _signature ?? Array.Empty<byte>() });

                if (!result.Accepted)
                {
                    await Shell.Current.DisplayAlert("Inspection not saved", ErrorText(result.Body), "OK");
                    return;
                }

                if (Requirement.Kind != "PullIn") Preferences.Set(PullOutOdometerKey, odometer);

                if (result.Outcome == Services.Offline.OutboxOutcome.Queued)
                    await Shell.Current.DisplayAlert("Inspection saved on the phone",
                        "There is no connection. The inspection, its photos and your signature are kept on the phone and will be sent automatically. You can continue.", "OK");

                await Shell.Current.GoToAsync("..", new Dictionary<string, object> { { "InspectionDone", odometer } });
            }
            finally
            {
                IsBusy = false;
            }
        }

        private List<string> Missing()
        {
            var missing = new List<string>();
            if (!long.TryParse(OdometerText?.Trim(), out var odometer) || odometer < 0) missing.Add("Odometer reading (miles)");
            else if (Requirement?.Kind == "PullIn" && Preferences.Get(PullOutOdometerKey, -1L) is long start && start >= 0 && odometer < start)
                missing.Add($"An odometer reading not below the Pull-out's ({start})");
            if (VinConfirmed is null) missing.Add("Confirm the VIN");
            if (VinConfirmed == false)
            {
                var vin = VinReported?.Trim() ?? "";
                if (vin.Length < 4 || !vin.All(char.IsLetterOrDigit)) missing.Add("The VIN on the vehicle (at least its last 4 characters)");
                if (VinPhoto is not { Length: > 0 }) missing.Add("A photo of the VIN");
            }
            foreach (var line in Lines.Where(l => !l.IsAnswered)) missing.Add(line.Label);
            foreach (var line in Lines.Where(l => l.IsProblem == true && !l.HasPhoto)) missing.Add($"Photo: {line.Label}");
            if (string.IsNullOrWhiteSpace(Comment)) missing.Add("Inspection comment");
            if (!IsSigned) missing.Add("Driver signature");
            return missing;
        }

        /// <summary>
        /// The Pull-out reading of this route and day, kept on the phone so the Pull-in is checked
        /// against it even without coverage: the server refuses a day that ends with fewer miles.
        /// </summary>
        private string PullOutOdometerKey => $"PullOutOdometer:{Event?.VehicleRouteId}:{Event?.Date:yyyyMMdd}";

        private static string ErrorText(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "The server did not accept the inspection.";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString() ?? body;
            }
            catch (System.Text.Json.JsonException)
            {
            }
            return "The server did not accept the inspection.";
        }

        /// <summary>The camera, a JPEG at most 1600 px on its long side. Null when the driver cancels.</summary>
        private static async Task<byte[]?> CaptureAsync()
        {
            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await Shell.Current.DisplayAlert("Camera", "This phone cannot take photos from the app.", "OK");
                    return null;
                }

                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert("Camera", "The camera permission is needed to take the photo.", "OK");
                    return null;
                }

                var file = await MediaPicker.Default.CapturePhotoAsync();
                if (file is null) return null;

                await using var stream = await file.OpenReadAsync();
                var image = PlatformImage.FromStream(stream);
                var resized = image.Width > 1600 || image.Height > 1600 ? image.Downsize(1600, disposeOriginal: true) : image;

                using var output = new MemoryStream();
                await resized.SaveAsync(output, ImageFormat.Jpeg, 0.7f);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Camera: {ex.Message}");
                await Shell.Current.DisplayAlert("Camera", "The photo could not be taken. Try again.", "OK");
                return null;
            }
        }
    }
}
