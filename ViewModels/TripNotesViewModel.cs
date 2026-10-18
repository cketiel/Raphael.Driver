using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services;
using System.Collections.ObjectModel;

namespace Raphael.Driver.ViewModels
{
    /// <summary>Sent when the notes of a trip changed, so the stop's detail shows what the driver now reads.</summary>
    public sealed record TripNotesChangedMessage(int TripId, string? PickupComment, string? DropoffComment);

    /// <summary>One note as a card on the notes page.</summary>
    public sealed class NoteItem
    {
        public int Id { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Meta { get; init; } = string.Empty;
        public bool IsToReview { get; init; }
        public bool IsCustomer { get; init; }
        public bool ForEntrance { get; init; }
    }

    /// <summary>
    /// The notes of one stop (RE-054): what the driver reads here, this trip's own note, the
    /// entrance's and the place's notes, and the patient's. The driver adds, edits and removes any
    /// of them; every change is saved at once, and with no connection the driver is told so.
    /// </summary>
    [QueryProperty(nameof(TripId), "TripId")]
    [QueryProperty(nameof(Side), "Side")]
    public partial class TripNotesViewModel : ObservableObject
    {
        private readonly ITripNotesService _notes;

        public TripNotesViewModel(ITripNotesService notes)
        {
            _notes = notes;
        }

        [ObservableProperty] private int _tripId;
        [ObservableProperty] private string _side = "Pickup";

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isLoaded;
        [ObservableProperty] private string _stopTitle = string.Empty;
        [ObservableProperty] private string? _driverReads;
        [ObservableProperty] private string? _placeName;
        [ObservableProperty] private string? _placeAddress;
        [ObservableProperty] private bool _hasPlace;
        [ObservableProperty] private bool _hasEntrance;
        [ObservableProperty] private string? _entranceName;
        [ObservableProperty] private string? _tripNote;
        [ObservableProperty] private bool _canEditTripNote;
        [ObservableProperty] private string? _patientName;
        [ObservableProperty] private string? _fileComment;
        [ObservableProperty] private bool _hasFileComment;

        public ObservableCollection<NoteItem> EntranceNotes { get; } = new();
        public ObservableCollection<NoteItem> PlaceNotes { get; } = new();
        public ObservableCollection<NoteItem> PatientNotes { get; } = new();

        private TripNotesDto? _last;

        partial void OnTripIdChanged(int value) => _ = LoadAsync();

        // The two query values arrive one after the other: whichever comes last redraws the right stop.
        partial void OnSideChanged(string value)
        {
            if (_last != null) Show(_last);
        }

        /// <summary>The big button at the bottom: which kind of note to add.</summary>
        [RelayCommand]
        private async Task AddAnyNoteAsync()
        {
            var options = new List<string>();
            if (HasPlace) options.Add("For this place");
            if (HasEntrance) options.Add("For this entrance");
            options.Add("About the patient");
            if (CanEditTripNote) options.Add("For this trip only");

            var choice = await Shell.Current.DisplayActionSheet("Add a note", "Cancel", null, options.ToArray());
            switch (choice)
            {
                case "For this place": await AddPlaceNoteAsync(); break;
                case "For this entrance": await AddEntranceNoteAsync(); break;
                case "About the patient": await AddPatientNoteAsync(); break;
                case "For this trip only": await EditTripNoteAsync(); break;
            }
        }

        [RelayCommand]
        private Task LoadAsync() => RunAsync(() => _notes.GetAsync(TripId), changed: false);

        [RelayCommand]
        private async Task EditTripNoteAsync()
        {
            if (!CanEditTripNote) return;
            var text = await Shell.Current.DisplayPromptAsync("This trip only",
                "Something that only holds for this trip, for example \"today, 3rd floor\".",
                "Save", "Cancel", maxLength: 2000, initialValue: TripNote ?? string.Empty);
            if (text == null) return;
            await RunAsync(() => _notes.SetTripNoteAsync(TripId, Side, text));
        }

        [RelayCommand]
        private Task AddPlaceNoteAsync() => AddAsync("New note for this place", forEntrance: false, customer: false);

        [RelayCommand]
        private Task AddEntranceNoteAsync() => AddAsync("New note for this entrance", forEntrance: true, customer: false);

        [RelayCommand]
        private Task AddPatientNoteAsync() => AddAsync("New note about the patient", forEntrance: false, customer: true);

        [RelayCommand]
        private async Task EditNoteAsync(NoteItem note)
        {
            if (note == null) return;
            var text = await Shell.Current.DisplayPromptAsync("Edit note", "Saving it also confirms it.",
                "Save", "Cancel", maxLength: 500, initialValue: note.Text);
            if (string.IsNullOrWhiteSpace(text) || text.Trim() == note.Text) return;
            await SaveNoteAsync(note, text.Trim());
        }

        [RelayCommand]
        private Task ConfirmNoteAsync(NoteItem note) => note == null ? Task.CompletedTask : SaveNoteAsync(note, note.Text);

        [RelayCommand]
        private async Task DeleteNoteAsync(NoteItem note)
        {
            if (note == null) return;
            var sure = await Shell.Current.DisplayAlert("Remove note",
                "It stops reaching the driver on every trip still to come.", "Remove", "Cancel");
            if (!sure) return;
            await RunAsync(() => note.IsCustomer
                ? _notes.RemoveCustomerNoteAsync(TripId, note.Id)
                : _notes.RemovePlaceNoteAsync(TripId, note.Id));
        }

        private async Task AddAsync(string title, bool forEntrance, bool customer)
        {
            var text = await Shell.Current.DisplayPromptAsync(title, null, "Add", "Cancel", maxLength: 500);
            if (string.IsNullOrWhiteSpace(text)) return;
            var dto = new NoteUpsertDto { Text = text.Trim(), Status = "Active", ForEntrance = forEntrance };
            await RunAsync(() => customer ? _notes.AddCustomerNoteAsync(TripId, dto) : _notes.AddPlaceNoteAsync(TripId, Side, dto));
        }

        private Task SaveNoteAsync(NoteItem note, string text)
        {
            var dto = new NoteUpsertDto { Text = text, Status = "Active", ForEntrance = note.ForEntrance };
            return RunAsync(() => note.IsCustomer
                ? _notes.UpdateCustomerNoteAsync(TripId, note.Id, dto)
                : _notes.UpdatePlaceNoteAsync(TripId, note.Id, dto));
        }

        private async Task RunAsync(Func<Task<NotesResult>> call, bool changed = true)
        {
            if (IsBusy || TripId <= 0) return;
            IsBusy = true;
            try
            {
                var result = await call();
                if (result.Offline)
                {
                    await Shell.Current.DisplayAlert("No connection",
                        "The notes could not be reached. Nothing was saved: try again when you have signal.", "OK");
                    return;
                }
                if (!result.Ok)
                {
                    await Shell.Current.DisplayAlert("Notes", result.Error ?? "The notes could not be saved.", "OK");
                    return;
                }

                Show(result.Notes!);
                if (changed)
                    WeakReferenceMessenger.Default.Send(new TripNotesChangedMessage(
                        TripId, result.Notes!.Pickup.Composed, result.Notes.Dropoff.Composed));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Show(TripNotesDto notes)
        {
            _last = notes;
            var stop = Side == "Dropoff" ? notes.Dropoff : notes.Pickup;

            StopTitle = Side == "Dropoff" ? "DROP-OFF NOTES" : "PICKUP NOTES";
            DriverReads = string.IsNullOrWhiteSpace(stop.Composed) ? "Nothing yet." : stop.Composed.Replace(" | ", Environment.NewLine);
            HasPlace = stop.Place != null;
            PlaceName = stop.Place?.Name;
            PlaceAddress = stop.Place?.Address;
            HasEntrance = stop.Place?.EntranceId != null;
            EntranceName = string.IsNullOrWhiteSpace(stop.Place?.EntranceLabel) ? "This entrance" : $"Entrance: {stop.Place!.EntranceLabel}";
            TripNote = stop.TripNote;
            CanEditTripNote = !notes.TripReadOnly;
            PatientName = notes.CustomerName;
            FileComment = stop.FileComment;
            HasFileComment = !string.IsNullOrWhiteSpace(stop.FileComment);

            Fill(EntranceNotes, stop.EntranceNotes, customer: false, forEntrance: true);
            Fill(PlaceNotes, stop.PlaceNotes, customer: false, forEntrance: false);
            Fill(PatientNotes, notes.CustomerNotes, customer: true, forEntrance: false);
            IsLoaded = true;
        }

        private static void Fill(ObservableCollection<NoteItem> target, IEnumerable<NoteDto> notes, bool customer, bool forEntrance)
        {
            target.Clear();
            foreach (var n in notes)
            {
                var who = n.UpdatedBy ?? n.CreatedBy;
                var source = n.Source switch { "Import" => "From the file", "Driver" => "Driver", "History" => "History", _ => "Office" };
                target.Add(new NoteItem
                {
                    Id = n.Id,
                    Text = n.Text,
                    Meta = n.Status == "ToReview" ? $"{source} · {who} · TO REVIEW" : $"{source} · {who}",
                    IsToReview = n.Status == "ToReview",
                    IsCustomer = customer,
                    ForEntrance = forEntrance,
                });
            }
        }
    }
}
