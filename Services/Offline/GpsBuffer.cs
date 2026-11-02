using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Raphael.Driver.DTOs;

namespace Raphael.Driver.Services.Offline
{
    /// <summary>
    /// The GPS fixes that could not be sent, kept on the phone and sent in batches when the
    /// connection comes back (RE-062).
    /// </summary>
    /// <remarks>
    /// The route-day metrics measure the miles with a patient on board on this track, so a stretch
    /// without coverage must not become a stretch without miles. One line per fix in one file, with a
    /// cap: a phone left without coverage for days keeps the most recent fixes, not the oldest.
    /// Static because the Android tracking service is created by the system, not by the app.
    /// </remarks>
    public static class GpsBuffer
    {
        private const int MaxFixes = 6000;  // fifty hours at one fix every thirty seconds
        private const int BatchSize = 200;

        private static readonly object Gate = new();
        private static readonly SemaphoreSlim Flushing = new(1, 1);
        private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "outbox", "gps.jsonl");

        public static int Count
        {
            get { lock (Gate) return File.Exists(FilePath) ? File.ReadLines(FilePath).Count() : 0; }
        }

        public static void Add(GpsDataDto fix)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    File.AppendAllText(FilePath, JsonSerializer.Serialize(fix) + Environment.NewLine);

                    var lines = File.ReadAllLines(FilePath);
                    if (lines.Length > MaxFixes) File.WriteAllLines(FilePath, lines[^MaxFixes..]);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GpsBuffer: could not keep a fix: {ex.Message}");
            }
        }

        /// <summary>Sends what is kept, oldest first, until it is empty or the network fails. Never throws.</summary>
        public static async Task FlushAsync(HttpClient http)
        {
            if (!await Flushing.WaitAsync(0)) return;
            try
            {
                while (true)
                {
                    List<string> batch;
                    lock (Gate)
                    {
                        if (!File.Exists(FilePath)) return;
                        batch = File.ReadLines(FilePath).Take(BatchSize).ToList();
                    }
                    if (batch.Count == 0) return;

                    var fixes = batch.Select(l => { try { return JsonSerializer.Deserialize<GpsDataDto>(l); } catch { return null; } })
                                     .Where(f => f is not null).ToList();

                    using var response = await http.PostAsJsonAsync("api/Gps/batch", fixes);
                    var status = (int)response.StatusCode;
                    if (!response.IsSuccessStatusCode && (status >= 500 || status is 401 or 408 or 429)) return;

                    // Taken, or refused for good (a bad batch must not block the rest): drop these lines.
                    lock (Gate)
                    {
                        var rest = File.ReadAllLines(FilePath).Skip(batch.Count).ToArray();
                        if (rest.Length == 0) File.Delete(FilePath);
                        else File.WriteAllLines(FilePath, rest);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GpsBuffer: flush stopped: {ex.Message}");
            }
            finally
            {
                Flushing.Release();
            }
        }
    }
}
