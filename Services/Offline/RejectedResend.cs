using System.Diagnostics;
using System.Text.Json;
using Raphael.Driver.DTOs;

namespace Raphael.Driver.Services.Offline
{
    /// <summary>
    /// Sends again what an older server refused and the phone moved aside (RE-092): a closing odometer
    /// refused in the past is accepted now, and the day it closes would otherwise be lost.
    /// </summary>
    /// <remarks>
    /// ⚠️ Safe before complete. A stop is sent again only while the server still has it as not performed and
    /// its trip is not cancelled, and over the server's own row, so nothing the office changed since is undone.
    /// Each change is tried once per app version. With nothing aside, which is the usual, it costs a
    /// directory listing.
    /// </remarks>
    public sealed class RejectedResend
    {
        /// <summary>As the server: older work is not accepted from the phone.</summary>
        private static readonly TimeSpan Oldest = TimeSpan.FromDays(7);
        private static readonly string[] Inspections = { "inspection", "inspection-photo", "inspection-signature" };

        private readonly Outbox _outbox;
        private readonly IScheduleService _schedules;
        private readonly SemaphoreSlim _running = new(1, 1);

        public RejectedResend(Outbox outbox, IScheduleService schedules)
        {
            _outbox = outbox;
            _schedules = schedules;
        }

        /// <summary>Never throws.</summary>
        public async Task RunAsync(string runLogin)
        {
            if (!_running.Wait(0)) return;
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

                var version = AppInfo.Current.VersionString;
                var items = _outbox.Rejected()
                    .Where(i => i.Request.RetriedVersion != version && DateTime.UtcNow - i.Request.CreatedAtUtc < Oldest).ToList();
                if (items.Count == 0) return;

                // The rows the server still has open, per day: what may be sent again.
                var open = new Dictionary<DateTime, Dictionary<int, ScheduleDto>>();
                async Task<ScheduleDto?> OpenRow(ScheduleDto body)
                {
                    if (body.Date is not DateTime date) return null;
                    if (!open.TryGetValue(date.Date, out var rows))
                        open[date.Date] = rows = (await _schedules.GetOpenNowAsync(runLogin, date.Date) ?? new List<ScheduleDto>())
                            .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
                    return rows.GetValueOrDefault(body.Id);
                }

                // Only the last state of each stop counts; the ones before it are part of it.
                var last = items.Where(i => i.Request.Kind == "schedule" && i.Request.ScheduleId is not null)
                    .GroupBy(i => i.Request.ScheduleId!.Value).ToDictionary(g => g.Key, g => g.Last().Meta);

                foreach (var (meta, request) in items)
                {
                    var body = _outbox.RejectedBody(meta);

                    if (Inspections.Contains(request.Kind))
                    {
                        request.Body = body;
                        await SendAsync(meta, request, version);
                        continue;
                    }

                    if (request.Kind != "schedule" || request.ScheduleId is not int id || last[id] != meta || body is null) continue;

                    var mine = JsonSerializer.Deserialize<ScheduleDto>(body, OutboxJson.Options);
                    if (mine is null || await OpenRow(mine) is not { } row || row.Status == "Canceled") continue;
                    if (!mine.Performed && (mine.Odometer is null || row.Odometer is not null)) continue;

                    // Over the server's row, only what the driver does.
                    row.Odometer = mine.Odometer ?? row.Odometer;
                    row.Arrive = mine.Arrive ?? row.Arrive;
                    row.ArriveDist = mine.ArriveDist ?? row.ArriveDist;
                    row.GPSArrive = mine.GPSArrive ?? row.GPSArrive;
                    row.Perform = mine.Perform ?? row.Perform;
                    row.PerformDist = mine.PerformDist ?? row.PerformDist;
                    row.Performed = mine.Performed;
                    row.ETA = mine.ETA ?? row.ETA;

                    request.Body = JsonSerializer.SerializeToUtf8Bytes(row, OutboxJson.Options);
                    await SendAsync(meta, request, version);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resend of refused changes: {ex.GetType().Name}");
            }
            finally
            {
                _running.Release();
            }
        }

        private async Task SendAsync(string meta, OutboxRequest request, string version)
        {
            request.RetriedVersion = version;
            _outbox.MarkRejected(meta, request);
            var result = await _outbox.SendAsync(request);
            if (result.Accepted || result.Status == 409 && request.Kind == "inspection") _outbox.ForgetRejected(meta);
        }
    }
}
