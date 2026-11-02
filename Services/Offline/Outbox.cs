using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Raphael.Driver.Services.Offline
{
    public enum OutboxOutcome
    {
        /// <summary>The server took it.</summary>
        Sent,

        /// <summary>No coverage, or the server could not take it now: kept on the phone and sent later.</summary>
        Queued,

        /// <summary>The server heard it and said no. Retrying would get the same answer.</summary>
        Rejected
    }

    public sealed record OutboxResult(OutboxOutcome Outcome, int? Status = null, string? Body = null)
    {
        /// <summary>The change will reach the server: now, or when the coverage comes back.</summary>
        public bool Accepted => Outcome != OutboxOutcome.Rejected;
    }

    /// <summary>A write to the server, as the outbox keeps it.</summary>
    public sealed class OutboxRequest
    {
        public string Method { get; set; } = "POST";

        public string Path { get; set; } = string.Empty;

        /// <summary>Null for a request with no body.</summary>
        public string? ContentType { get; set; }

        /// <summary>What it is, for reading pending changes back (<c>schedule</c>, <c>inspection</c>…).</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The Schedule row it changes, when it changes one.</summary>
        public int? ScheduleId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public int Attempts { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public byte[]? Body { get; set; }

        public static OutboxRequest Json(string method, string path, object body, string kind, int? scheduleId = null) => new()
        {
            Method = method,
            Path = path,
            ContentType = "application/json",
            Body = JsonSerializer.SerializeToUtf8Bytes(body, OutboxJson.Options),
            Kind = kind,
            ScheduleId = scheduleId
        };

        public static OutboxRequest Empty(string method, string path, string kind) => new() { Method = method, Path = path, Kind = kind };

        public static OutboxRequest Bytes(string method, string path, byte[] body, string contentType, string kind) => new()
        {
            Method = method,
            Path = path,
            ContentType = contentType,
            Body = body,
            Kind = kind
        };
    }

    internal static class OutboxJson
    {
        public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    }

    /// <summary>
    /// Every change the driver makes goes to the server through here (RE-062). With no coverage it is
    /// kept on the phone, in order, and sent as soon as the connection comes back.
    /// </summary>
    /// <remarks>
    /// ⚠️ Order is the point. A Perform kept while the odometer before it is still waiting would reach
    /// the server first and be refused, so once anything is waiting, everything after it waits too.
    ///
    /// <para>
    /// Kept as files in the app's own storage (never shared, removed with the app): one pair per
    /// change, the description and the body, named by the moment it was made. A change the server
    /// refuses outright (a 4xx other than 401/408/429) is not retried forever: it is moved aside to
    /// <c>outbox/rejected</c> and the queue goes on.
    /// </para>
    ///
    /// <para>
    /// Not kept, on purpose: signing in and token renewal, the "Call me" request and the SMS to the
    /// patient (sent hours later they would mislead), push-token registration (redone at every start)
    /// and arrival-time updates (worked out from the network, and stale by the time it is back).
    /// </para>
    /// </remarks>
    public sealed class Outbox
    {
        private readonly HttpClient _http;
        private readonly string _folder;
        private readonly string _rejected;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly SemaphoreSlim _flushing = new(1, 1);
        private Timer? _timer;
        private long _sequence;

        public Outbox(IHttpClientFactory factory)
        {
            _http = factory.CreateClient(nameof(Outbox));
            _folder = Path.Combine(FileSystem.AppDataDirectory, "outbox");
            _rejected = Path.Combine(_folder, "rejected");
            Directory.CreateDirectory(_rejected);
            _sequence = DateTime.UtcNow.Ticks;

            Connectivity.ConnectivityChanged += (_, e) =>
            {
                if (e.NetworkAccess == NetworkAccess.Internet) _ = FlushAsync();
            };
            _timer = new Timer(_ => { if (PendingCount > 0) _ = FlushAsync(); }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>Raised when changes are kept or delivered. May come from a background thread.</summary>
        public event EventHandler? Changed;

        /// <summary>Raised when a change could not be sent and was kept on the phone.</summary>
        public event EventHandler? Kept;

        public int PendingCount => Directory.Exists(_folder) ? Directory.GetFiles(_folder, "*.json").Length : 0;

        /// <summary>
        /// Sends the change now if it can, keeps it otherwise. Never throws.
        /// </summary>
        public async Task<OutboxResult> SendAsync(OutboxRequest request)
        {
            await _gate.WaitAsync();
            try
            {
                // Something is already waiting: this one goes behind it, never ahead.
                if (PendingCount > 0)
                {
                    Keep(request);
                    _ = FlushAsync();
                    return new OutboxResult(OutboxOutcome.Queued);
                }

                var attempt = await TrySendAsync(request);
                if (attempt.Outcome == OutboxOutcome.Queued) Keep(request);
                return attempt;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>The bodies of the changes still waiting, of one kind, oldest first.</summary>
        public List<(OutboxRequest Request, T Body)> Pending<T>(string kind)
        {
            var list = new List<(OutboxRequest, T)>();
            foreach (var (meta, request) in Items())
            {
                if (request.Kind != kind) continue;
                try
                {
                    var body = File.ReadAllBytes(BodyFile(meta));
                    var value = JsonSerializer.Deserialize<T>(body, OutboxJson.Options);
                    if (value is not null) list.Add((request, value));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Outbox: could not read {meta}: {ex.Message}");
                }
            }
            return list;
        }

        /// <summary>Sends what is waiting, oldest first, until the queue is empty or the network fails.</summary>
        public async Task FlushAsync()
        {
            if (!await _flushing.WaitAsync(0)) return;
            var delivered = false;
            try
            {
                foreach (var (meta, request) in Items())
                {
                    try
                    {
                        request.Body = File.Exists(BodyFile(meta)) ? await File.ReadAllBytesAsync(BodyFile(meta)) : null;
                    }
                    catch (IOException)
                    {
                        return;
                    }

                    var result = await TrySendAsync(request);
                    if (result.Outcome == OutboxOutcome.Queued)
                    {
                        request.Attempts++;
                        WriteMeta(meta, request);
                        return; // still no way through: try again later, in the same order
                    }

                    if (result.Outcome == OutboxOutcome.Rejected)
                    {
                        Debug.WriteLine($"Outbox: {request.Method} {request.Path} refused with {result.Status}; moved aside.");
                        MoveAside(meta);
                    }
                    else
                    {
                        Delete(meta);
                    }
                    delivered = true;
                }
            }
            finally
            {
                _flushing.Release();
                if (delivered) Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task<OutboxResult> TrySendAsync(OutboxRequest request)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return new OutboxResult(OutboxOutcome.Queued);

            try
            {
                using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Path);
                if (request.Body is not null)
                {
                    message.Content = new ByteArrayContent(request.Body);
                    if (request.ContentType is not null)
                        message.Content.Headers.ContentType = request.ContentType == "application/json"
                            ? new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName }
                            : new MediaTypeHeaderValue(request.ContentType);
                }

                using var response = await _http.SendAsync(message);
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                    return new OutboxResult(OutboxOutcome.Sent, status, await response.Content.ReadAsStringAsync());

                // The server could not take it now, or the session needs signing in again: keep it.
                if (status >= 500 || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
                    return new OutboxResult(OutboxOutcome.Queued, status);

                return new OutboxResult(OutboxOutcome.Rejected, status, await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                return new OutboxResult(OutboxOutcome.Queued);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Outbox: unexpected failure sending {request.Path}: {ex.Message}");
                return new OutboxResult(OutboxOutcome.Queued);
            }
        }

        private void Keep(OutboxRequest request)
        {
            var name = Interlocked.Increment(ref _sequence).ToString("D20");
            var meta = Path.Combine(_folder, name + ".json");
            if (request.Body is not null) File.WriteAllBytes(BodyFile(meta), request.Body);
            WriteMeta(meta, request);
            Changed?.Invoke(this, EventArgs.Empty);
            Kept?.Invoke(this, EventArgs.Empty);
        }

        private IEnumerable<(string Meta, OutboxRequest Request)> Items()
        {
            if (!Directory.Exists(_folder)) yield break;
            foreach (var meta in Directory.GetFiles(_folder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                OutboxRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<OutboxRequest>(File.ReadAllText(meta), OutboxJson.Options);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Outbox: unreadable item {meta}: {ex.Message}");
                }
                if (request is not null) yield return (meta, request);
            }
        }

        private static string BodyFile(string meta) => Path.ChangeExtension(meta, ".bin");

        private static void WriteMeta(string meta, OutboxRequest request)
        {
            var temp = meta + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(request, OutboxJson.Options));
            File.Move(temp, meta, overwrite: true);
        }

        private static void Delete(string meta)
        {
            File.Delete(meta);
            if (File.Exists(BodyFile(meta))) File.Delete(BodyFile(meta));
        }

        private void MoveAside(string meta)
        {
            var target = Path.Combine(_rejected, Path.GetFileName(meta));
            File.Move(meta, target, overwrite: true);
            if (File.Exists(BodyFile(meta))) File.Move(BodyFile(meta), Path.ChangeExtension(target, ".bin"), overwrite: true);
        }
    }
}
