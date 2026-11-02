using Raphael.Driver.DTOs;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Raphael.Driver.Services
{
    /// <summary>The answer of a notes call: the whole notes of the trip again, or why not.</summary>
    public sealed record NotesResult(TripNotesDto? Notes, string? Error, bool Offline, bool Queued = false)
    {
        public bool Ok => Notes != null;
    }

    /// <summary>
    /// The notes of a trip's places and patient (RE-054): what the driver reads at each stop, and
    /// what the driver adds, edits or removes there.
    /// </summary>
    /// <remarks>
    /// Every call answers with the trip's whole notes, so the page never works out on its own what
    /// will show. Without coverage a change is kept on the phone and sent when it comes back (RE-062);
    /// it shows once the server has it. Note texts are PHI: never logged.
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
        private readonly Offline.Outbox _outbox;

        public TripNotesService(HttpClient http, Offline.Outbox outbox)
        {
            _http = http;
            _outbox = outbox;
        }

        private static string Notes(int tripId) => $"api/trips/{tripId}/notes";

        public Task<NotesResult> GetAsync(int tripId) =>
            SendAsync(() => _http.GetAsync(Notes(tripId)));

        public Task<NotesResult> SetTripNoteAsync(int tripId, string side, string? text) =>
            WriteAsync("PUT", $"{Notes(tripId)}/{side}/trip-note", new TripNoteUpdateDto { Text = text });

        public Task<NotesResult> AddPlaceNoteAsync(int tripId, string side, NoteUpsertDto note) =>
            WriteAsync("POST", $"{Notes(tripId)}/{side}/place-notes", note);

        public Task<NotesResult> UpdatePlaceNoteAsync(int tripId, int noteId, NoteUpsertDto note) =>
            WriteAsync("PUT", $"{Notes(tripId)}/place-notes/{noteId}", note);

        public Task<NotesResult> RemovePlaceNoteAsync(int tripId, int noteId) =>
            WriteAsync("DELETE", $"{Notes(tripId)}/place-notes/{noteId}", null);

        public Task<NotesResult> AddCustomerNoteAsync(int tripId, NoteUpsertDto note) =>
            WriteAsync("POST", $"{Notes(tripId)}/customer-notes", note);

        public Task<NotesResult> UpdateCustomerNoteAsync(int tripId, int noteId, NoteUpsertDto note) =>
            WriteAsync("PUT", $"{Notes(tripId)}/customer-notes/{noteId}", note);

        public Task<NotesResult> RemoveCustomerNoteAsync(int tripId, int noteId) =>
            WriteAsync("DELETE", $"{Notes(tripId)}/customer-notes/{noteId}", null);

        /// <summary>
        /// A change through the outbox: with coverage the server's answer is the whole notes again;
        /// without it the change is kept and the page is told so.
        /// </summary>
        private async Task<NotesResult> WriteAsync(string method, string path, object? body)
        {
            var request = body is null
                ? Offline.OutboxRequest.Empty(method, path, "notes")
                : Offline.OutboxRequest.Json(method, path, body, "notes");

            var result = await _outbox.SendAsync(request);
            switch (result.Outcome)
            {
                case Offline.OutboxOutcome.Queued:
                    return new NotesResult(null, null, Offline: true, Queued: true);
                case Offline.OutboxOutcome.Sent:
                    try
                    {
                        return new NotesResult(JsonSerializer.Deserialize<TripNotesDto>(result.Body ?? "", Json), null, false);
                    }
                    catch (JsonException)
                    {
                        return new NotesResult(null, "The notes could not be read.", false);
                    }
                default:
                    string? message = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(result.Body ?? "");
                        if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
                        else if (doc.RootElement.TryGetProperty("title", out var t)) message = t.GetString();
                    }
                    catch
                    {
                        // Not JSON: the status is all there is to say.
                    }
                    Debug.WriteLine($"TripNotesService: the server answered {result.Status}.");
                    return new NotesResult(null, message ?? $"The server answered {result.Status}.", false);
            }
        }

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
