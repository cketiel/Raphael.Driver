using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;

namespace Raphael.Driver.ViewModels
{
    /// <summary>One line of the Documents page: a document type and where it stands.</summary>
    public sealed partial class ComplianceRow : ObservableObject
    {
        /// <summary>The file is on its way from the server: the View button shows a spinner.</summary>
        [ObservableProperty, NotifyPropertyChangedFor(nameof(IsIdle))] private bool _isOpening;
        public bool IsIdle => !IsOpening;

        public string ActionGlyph => Item.Current is not null ? "" : "";   // rotate / upload

        public required ComplianceItemDto Item { get; init; }
        public required ComplianceSubjectDto Owner { get; init; }

        public string Name => Item.Type.Name;
        public string Kind => Item.Type.Category switch { "COURSE" => "Course", "PHOTOS" => "5 photos", _ => "Document" };

        /// <summary>valid, soon, expired, pending, rejected or missing: the colour of the chip.</summary>
        public required string State { get; init; }
        public required string StateText { get; init; }

        /// <summary>The chip's icon: check, warning, clock, x, or a dash for nothing uploaded.</summary>
        public string StateGlyph => State switch
        {
            "valid" => "",
            "soon" => "",
            "pending" => "",
            "expired" => "",
            "rejected" => "",
            _ => ""
        };
        public required string Detail { get; init; }

        /// <summary>A second line when there is more to say: the rejection reason, or a renewal waiting.</summary>
        public string? Note { get; init; }
        public bool HasNote => !string.IsNullOrEmpty(Note);

        public string ActionText => Item.Current is not null ? "Renew" : Item.Latest?.Status == "PENDING" ? "Replace" : "Upload";
        public bool CanUpload => Owner.CanUpload;

        /// <summary>The document whose file the driver can look at: the approved one, else the latest.</summary>
        public ComplianceDocumentDto? Viewable => Item.Current ?? Item.Latest;
        public bool CanView => Viewable?.Files.Count > 0;

        /// <summary>What the file is, at a glance: a PDF, an image, or the five photos.</summary>
        public string FileGlyph => Viewable?.Files switch
        {
            { Count: > 1 } => "",                                              // images
            [{ ContentType: "application/pdf" }] => "",                        // file-pdf
            { Count: 1 } => "",                                                // file-image
            _ => ""                                                            // folder-plus: nothing yet
        };
        public string FileLabel => Viewable?.Files switch
        {
            { Count: > 1 } files => $"{files.Count} photos",
            [{ ContentType: "application/pdf" }] => "PDF",
            [{ ContentType: "image/png" }] => "PNG image",
            { Count: 1 } => "JPG image",
            _ => "No file yet"
        };

        /// <summary>The expiry line beside the calendar, and how much of a year is left (the bar).</summary>
        public string? ExpiryText { get; init; }
        public bool HasExpiry => ExpiryText is not null;
        /// <summary>The bar only for a document that counts: an approved one.</summary>
        public bool HasBar => HasExpiry && State is "valid" or "soon" or "expired" && Item.Current is not null;
        public string ExpiryCaption { get; init; } = "EXPIRES";
        public double ExpiryProgress { get; init; }

        public string Glyph => Item.Type.Code switch
        {
            "INSURANCE" => "",          // shield
            "REGISTRATION" => "",       // file
            "VEHICLE_PHOTOS" => "",     // camera
            "DRIVER_LICENSE" => "",     // id card
            "BACKGROUND_CHECK" => "",   // magnifier
            "DRUG_TEST" => "",          // flask
            "DOT_MEDICAL" => "",        // user doctor
            _ => ""                     // graduation cap: the courses
        };
    }

    /// <summary>
    /// Settings › MY DOCUMENTS (RE-065): the driver's own documents and those of the vehicle of
    /// their route, each with where it stands, and the way to upload or renew it.
    /// </summary>
    public partial class DocumentsViewModel : ObservableObject
    {
        /// <summary>Days before the expiry a document shows as expiring: the server's first warning.</summary>
        public const int SoonDays = 30;

        private readonly IComplianceService _compliance;
        private MyComplianceDto? _data;

        public ObservableCollection<ComplianceRow> Rows { get; } = new();

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string? _error;
        [ObservableProperty] private bool _showingVehicle;

        /// <summary>me or vehicle: the tabs (SegmentedTabs) bind to it.</summary>
        [ObservableProperty] private string _tab = "me";
        partial void OnTabChanged(string value)
        {
            ShowingVehicle = value == "vehicle";
            Show();
        }

        /// <summary>Whose the three counters are: "My documents" or the vehicle's.</summary>
        [ObservableProperty] private string _summaryTitle = "My documents";
        [ObservableProperty] private string _summaryGlyph = "";
        [ObservableProperty] private int _totalCount;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMeAttention))] private int _meAttention;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(HasVehicleAttention))] private int _vehicleAttention;
        public bool HasMeAttention => MeAttention > 0;
        public bool HasVehicleAttention => VehicleAttention > 0;
        [ObservableProperty] private string? _subjectLine;

        /// <summary>The header: whose documents these are.</summary>
        [ObservableProperty] private string _driverName = "My documents";
        [ObservableProperty] private string _initials = "";
        public string Username { get; } = Preferences.Get("Username", string.Empty);

        /// <summary>The three counters of the tab on screen.</summary>
        [ObservableProperty] private int _validCount;
        [ObservableProperty] private int _attentionCount;
        [ObservableProperty] private int _reviewCount;
        [ObservableProperty] private bool _hasVehicle = true;

        private int _loadedVersion = -1;

        public DocumentsViewModel(IComplianceService compliance)
        {
            _compliance = compliance;
        }

        /// <summary>Loads the first time and after an upload; pull to refresh loads any time.</summary>
        public async Task LoadIfNeededAsync()
        {
            if (_loadedVersion == ComplianceService.Version) return;
            await LoadAsync();
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            Error = null;
            try
            {
                var result = await _compliance.GetMineAsync();
                if (!result.Ok) { Error = result.Error; return; }
                _data = result.Value;
                _loadedVersion = ComplianceService.Version;
                MeAttention = Attention(_data!.Driver);
                VehicleAttention = _data.Vehicle is null ? 0 : Attention(_data.Vehicle);
                DriverName = _data.Driver.SubjectName ?? "My documents";
                Initials = InitialsOf(DriverName);
                Show();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectTab(string tab) => Tab = tab;

        [RelayCommand]
        private async Task OpenAsync(ComplianceRow row)
        {
            if (!row.CanUpload) return;
            await Shell.Current.GoToAsync(nameof(Views.DocumentUploadPage), new Dictionary<string, object> { ["Row"] = row });
        }

        [RelayCommand]
        private async Task ViewAsync(ComplianceRow row)
        {
            var doc = row.Viewable;
            var file = doc?.Files.FirstOrDefault();
            if (doc is null || file is null || row.IsOpening) return;

            // The vehicle photos: the driver picks which one.
            if (doc.Files.Count > 1)
            {
                var pick = await Shell.Current.DisplayActionSheet(row.Name, "Cancel", null,
                    doc.Files.Select(f => SlotName(f.Slot)).ToArray());
                file = doc.Files.FirstOrDefault(f => SlotName(f.Slot) == pick);
                if (file is null) return;
            }

            ComplianceResult<string> path;
            row.IsOpening = true;
            try
            {
                path = await _compliance.DownloadAsync(doc.Id, file.Slot, file.ContentType);
            }
            finally
            {
                row.IsOpening = false;
            }
            if (!path.Ok)
            {
                await Shell.Current.DisplayAlert(row.Name, path.Error, "OK");
                return;
            }
            await Launcher.Default.OpenAsync(new OpenFileRequest(row.Name, new ReadOnlyFile(path.Value!, file.ContentType)));
        }

        public static string SlotName(string slot) => slot switch
        {
            "FRONT" => "Front",
            "BACK" => "Back",
            "RIGHT" => "Right side",
            "LEFT" => "Left side",
            "INTERIOR" => "Inside",
            _ => "Page"
        };

        private void Show()
        {
            Rows.Clear();
            if (_data is null) return;
            var subject = ShowingVehicle ? _data.Vehicle : _data.Driver;
            HasVehicle = !ShowingVehicle || _data.Vehicle is not null;
            SubjectLine = ShowingVehicle
                ? _data.Vehicle is null ? null : $"{_data.Vehicle.SubjectName} · vehicle of your route"
                : "Your license, checks and courses";
            ValidCount = AttentionCount = ReviewCount = TotalCount = 0;
            SummaryGlyph = ShowingVehicle ? "" : "";
            SummaryTitle = ShowingVehicle
                ? _data.Vehicle is null ? "Vehicle documents" : $"{_data.Vehicle.SubjectName} documents"
                : "My documents";
            if (subject is null) return;
            foreach (var item in subject.Items) Rows.Add(RowOf(item, subject));
            ValidCount = Rows.Count(r => r.State == "valid");
            AttentionCount = Rows.Count(r => r.State is "soon" or "expired" or "rejected");
            ReviewCount = Rows.Count(r => r.State == "pending");
            TotalCount = Rows.Count;
        }

        /// <summary>How many need the driver: expiring, expired or rejected. The tab's badge.</summary>
        private static int Attention(ComplianceSubjectDto subject) => subject.Items.Count(i =>
            (i.Current is not null && i.Current.DaysToExpiry <= SoonDays) || i.Latest?.Status == "REJECTED");

        /// <summary>"Roger García" → "RG".</summary>
        public static string InitialsOf(string? name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
            };
        }

        private static ComplianceRow RowOf(ComplianceItemDto item, ComplianceSubjectDto owner)
        {
            var current = item.Current;
            var latest = item.Latest;

            if (current is not null)
            {
                var days = current.DaysToExpiry;
                var (state, text) = days < 0 ? ("expired", "Expired") : days <= SoonDays ? ("soon", "Expiring") : ("valid", "Valid");
                var when = days < 0 ? $"{-days} day{(days == -1 ? "" : "s")} ago"
                    : days == 0 ? "expires today" : days == 1 ? "expires tomorrow" : $"{days} days left";
                var note = latest?.Status switch
                {
                    "PENDING" => $"Renewal sent, waiting for approval (expires {latest.ExpiresOn:MMM d, yyyy}).",
                    "REJECTED" => $"Renewal rejected: {latest.RejectionReason}",
                    _ => null
                };
                return new ComplianceRow
                {
                    Item = item, Owner = owner, State = latest?.Status == "REJECTED" ? "rejected" : state, StateText = text,
                    Detail = when, Note = note,
                    ExpiryText = $"{current.ExpiresOn:MMM d, yyyy}", ExpiryCaption = days < 0 ? "EXPIRED" : "EXPIRES",
                    ExpiryProgress = Math.Clamp(days / 365.0, 0, 1)
                };
            }

            if (latest?.Status == "PENDING")
                return new ComplianceRow
                {
                    Item = item, Owner = owner, State = "pending", StateText = "In review",
                    Detail = $"Sent {latest.UploadedAtUtc.ToLocalTime():MMM d}",
                    Note = "Not valid until an admin approves it.",
                    ExpiryText = $"{latest.ExpiresOn:MMM d, yyyy}"
                };

            if (latest?.Status == "REJECTED")
                return new ComplianceRow
                {
                    Item = item, Owner = owner, State = "rejected", StateText = "Rejected",
                    Detail = $"Reviewed {latest.ReviewedAtUtc?.ToLocalTime():MMM d}", Note = latest.RejectionReason
                };

            return new ComplianceRow
            {
                Item = item, Owner = owner, State = "missing", StateText = "Not uploaded",
                Detail = item.Type.IsRequired ? "Required" : "Optional"
            };
        }
    }
}
