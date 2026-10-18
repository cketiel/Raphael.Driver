using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raphael.Driver.Services
{
    /// <summary>The answer of a notes call: the whole notes of the trip again, or why not.</summary>
    public sealed record NotesResult(TripNotesDto? Notes, string? Error, bool Offline)
    {
        public bool Ok => Notes != null;
    }

    /// <summary>
    /// The notes of a trip's places and patient (RE-054): what the driver reads at each stop, and
    /// what the driver adds, edits or removes there.
    /// </summary>
    /// <remarks>
    /// Every call answers with the trip's whole notes, so the page never works out on its own what
    /// will show. Nothing is kept on the phone to send later: with no connection the driver is told
    /// so and tries again. Note texts are PHI: never logged.
    /// </remarks>
    public interface ITripNotesService
    {
        Task<NotesResult> GetAsync(int tripId);
        Task<NotesResult> SetTripNoteAsync(int tripId, string side, string? text);
        Task<NotesResult> AddPlaceNoteAsync(int tripId, string side, NoteUpsertDto note);
        Task<NotesResult> UpdatePlaceNoteAsync(int tripId, int noteId, NoteUpsertDto note);
        Task<NotesResult> RemovePlaceNoteAsync(int tripId, int noteId);
        Task<NotesResult> AddCustomerNoteAsync(int tripId, NoteUpsertDto note);
        Task<NotesResult> UpdateCustomerNoteAsync(int tripId, int noteId, NoteUpsertDto note);
        Task<NotesResult> RemoveCustomerNoteAsync(int tripId, int noteId);
    }

    public class TripNotesService : ITripNotesService
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _http;

        public TripNotesService(HttpClient http)
        {
            _http = http;
        }

        private static string Notes(int tripId) => $"api/trips/{tripId}/notes";

        public Task<NotesResult> GetAsync(int tripId) =>
            SendAsync(() => _http.GetAsync(Notes(tripId)));

        public Task<NotesResult> SetTripNoteAsync(int tripId, string side, string? text) =>
            SendAsync(() => _http.PutAsJsonAsync($"{Notes(tripId)}/{side}/trip-note", new TripNoteUpdateDto { Text = text }));

        public Task<NotesResult> AddPlaceNoteAsync(int tripId, string side, NoteUpsertDto note) =>
            SendAsync(() => _http.PostAsJsonAsync($"{Notes(tripId)}/{side}/place-notes", note));

        public Task<NotesResult> UpdatePlaceNoteAsync(int tripId, int noteId, NoteUpsertDto note) =>
            SendAsync(() => _http.PutAsJsonAsync($"{Notes(tripId)}/place-notes/{noteId}", note));

        public Task<NotesResult> RemovePlaceNoteAsync(int tripId, int noteId) =>
            SendAsync(() => _http.DeleteAsync($"{Notes(tripId)}/place-notes/{noteId}"));

        public Task<NotesResult> AddCustomerNoteAsync(int tripId, NoteUpsertDto note) =>
            SendAsync(() => _http.PostAsJsonAsync($"{Notes(tripId)}/customer-notes", note));

        public Task<NotesResult> UpdateCustomerNoteAsync(int tripId, int noteId, NoteUpsertDto note) =>
            SendAsync(() => _http.PutAsJsonAsync($"{Notes(tripId)}/customer-notes/{noteId}", note));

        public Task<NotesResult> RemoveCustomerNoteAsync(int tripId, int noteId) =>
            SendAsync(() => _http.DeleteAsync($"{Notes(tripId)}/customer-notes/{noteId}"));

        private static async Task<NotesResult> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return new NotesResult(null, null, Offline: true);

            try
            {
                var response = await send();
                if (response.IsSuccessStatusCode)
                    return new NotesResult(await response.Content.ReadFromJsonAsync<TripNotesDto>(Json), null, false);

                // 400 carries { message } (never note text); anything else is said plainly.
                string? message = null;
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
                    else if (doc.RootElement.TryGetProperty("title", out var t)) message = t.GetString();
                }
                catch
                {
                    // Not JSON: the status is all there is to say.
                }

                Debug.WriteLine($"TripNotesService: the server answered {(int)response.StatusCode}.");
                return new NotesResult(null, message ?? $"The server answered {(int)response.StatusCode}.", false);
            }
            catch (HttpRequestException)
            {
                return new NotesResult(null, null, Offline: true);
            }
            catch (TaskCanceledException)
            {
                return new NotesResult(null, null, Offline: true);
            }
        }
    }
}
