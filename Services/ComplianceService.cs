using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Raphael.Driver.DTOs;

namespace Raphael.Driver.Services
{
    /// <summary>The answer of a compliance call: the value, or a sentence for the driver.</summary>
    public sealed record ComplianceResult<T>(T? Value, string? Error)
    {
        public bool Ok => Error is null;
    }

    /// <summary>
    /// The driver's compliance documents and those of the vehicle of their route (RE-065).
    /// </summary>
    /// <remarks>
    /// Online only, unlike the inspection: a document is uploaded once in a while, from wherever
    /// there is coverage, and nothing of the route waits for it. The files are employee and health
    /// records: never logged, and only kept on the phone while they are shown.
    /// </remarks>
    public interface IComplianceService
    {
        Task<ComplianceResult<MyComplianceDto>> GetMineAsync();

        /// <summary>Creates the document, then uploads each file. Submitted for review once all are up.</summary>
        /// <param name="progress">Told how many files are up, so the screen can say "2 of 5".</param>
        Task<ComplianceResult<ComplianceDocumentDto>> UploadAsync(ComplianceDocumentCreateDto document,
            IReadOnlyList<(string Slot, byte[] Content, string ContentType)> files, IProgress<int>? progress = null);

        /// <summary>Downloads a file to the cache and returns its path, to open with the phone's viewer.</summary>
        Task<ComplianceResult<string>> DownloadAsync(int documentId, string slot, string contentType);

        /// <summary>Raised after an upload, so the badge and the banner count again.</summary>
        event EventHandler? Changed;
    }

    public sealed class ComplianceService : IComplianceService
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
        private readonly HttpClient _http;

        // Static: the service is created per consumer (AddHttpClient), and an upload made through one
        // instance has to reach the page, the Settings card and the Dashboard banner holding others.
        private static event EventHandler? AnyChanged;

        /// <summary>Goes up with every upload: a screen that loaded an older one loads again.</summary>
        public static int Version { get; private set; }

        public event EventHandler? Changed
        {
            add => AnyChanged += value;
            remove => AnyChanged -= value;
        }

        public ComplianceService(HttpClient http)
        {
            _http = http;
        }

        public async Task<ComplianceResult<MyComplianceDto>> GetMineAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/compliance/me");
                if (!response.IsSuccessStatusCode) return new(null, await ErrorOfAsync(response));
                return new(await response.Content.ReadFromJsonAsync<MyComplianceDto>(Json), null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Compliance: {ex.GetType().Name}");
                return new(null, "No connection. Try again when you have signal.");
            }
        }

        public async Task<ComplianceResult<ComplianceDocumentDto>> UploadAsync(ComplianceDocumentCreateDto document,
            IReadOnlyList<(string Slot, byte[] Content, string ContentType)> files, IProgress<int>? progress = null)
        {
            try
            {
                var created = await _http.PostAsJsonAsync("api/compliance/documents", document);
                if (!created.IsSuccessStatusCode) return new(null, await ErrorOfAsync(created));
                var doc = await created.Content.ReadFromJsonAsync<ComplianceDocumentDto>(Json);
                if (doc is null) return new(null, "The server did not answer.");

                var sent = 0;
                foreach (var (slot, content, contentType) in files)
                {
                    progress?.Report(sent++);
                    using var body = new ByteArrayContent(content);
                    body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    var put = await _http.PutAsync($"api/compliance/documents/{doc.Id}/files/{slot}", body);
                    if (!put.IsSuccessStatusCode) return new(null, await ErrorOfAsync(put));
                    doc = await put.Content.ReadFromJsonAsync<ComplianceDocumentDto>(Json) ?? doc;
                }

                Version++;
                AnyChanged?.Invoke(this, EventArgs.Empty);
                return new(doc, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Compliance upload: {ex.GetType().Name}");
                return new(null, "The upload did not finish. Check your signal and try again.");
            }
        }

        public async Task<ComplianceResult<string>> DownloadAsync(int documentId, string slot, string contentType)
        {
            try
            {
                var response = await _http.GetAsync($"api/compliance/documents/{documentId}/files/{slot}");
                if (!response.IsSuccessStatusCode) return new(null, await ErrorOfAsync(response));

                // One file at a time in the cache: the previous one goes as soon as another is opened.
                var folder = Path.Combine(FileSystem.CacheDirectory, "compliance");
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                Directory.CreateDirectory(folder);
                var extension = contentType == "application/pdf" ? "pdf" : contentType == "image/png" ? "png" : "jpg";
                var path = Path.Combine(folder, $"document-{documentId}-{slot.ToLowerInvariant()}.{extension}");
                await File.WriteAllBytesAsync(path, await response.Content.ReadAsByteArrayAsync());
                return new(path, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Compliance download: {ex.GetType().Name}");
                return new(null, "No connection. Try again when you have signal.");
            }
        }

        private static async Task<string> ErrorOfAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return "You can only upload your own documents and those of your route's vehicle.";
            try
            {
                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } message) return message;
            }
            catch (JsonException)
            {
            }
            return "The server did not accept it. Try again.";
        }
    }
}
