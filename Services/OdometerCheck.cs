using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Raphael.Driver.DTOs;
using Raphael.Driver.Helpers;

namespace Raphael.Driver.Services
{
    /// <summary>Warns about an odometer reading that does not add up, before it is saved (RE-092).</summary>
    public interface IOdometerCheck
    {
        /// <summary>Asks the server, in the background, what this garage event is compared with. Never throws.</summary>
        Task PrefetchAsync(int scheduleId);

        /// <summary>True to go on and save. The driver may always save: this only asks.</summary>
        Task<bool> ConfirmAsync(ScheduleDto garageEvent, long reading);

        /// <summary>Keeps the day's Pull-out reading on the phone, for the Pull-in without coverage.</summary>
        void RememberPullOut(ScheduleDto garageEvent, long reading);
    }

    /// <remarks>
    /// ⚠️ It never waits for the network: the comparison is fetched when the page opens, and if it has not
    /// come by the time of saving, the reading is checked with what the phone knows, or not at all.
    /// </remarks>
    public sealed class OdometerCheck : IOdometerCheck
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _factory;
        private readonly ConcurrentDictionary<int, OdometerReference> _references = new();

        public OdometerCheck(IHttpClientFactory factory) => _factory = factory;

        public async Task PrefetchAsync(int scheduleId)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            try
            {
                var response = await _factory.CreateClient(nameof(Offline.Outbox)).GetAsync($"api/odometer/schedules/{scheduleId}/reference");
                if (!response.IsSuccessStatusCode) return;
                if (JsonSerializer.Deserialize<OdometerReference>(await response.Content.ReadAsStringAsync(), Json) is { } reference)
                    _references[scheduleId] = reference;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Odometer reference: {ex.Message}");
            }
        }

        public async Task<bool> ConfirmAsync(ScheduleDto garageEvent, long reading)
        {
            string? warning;
            try
            {
                var isPullIn = garageEvent.Name == "Pull-in";
                var onPhone = isPullIn && Preferences.Get(Key(garageEvent), -1L) is var kept && kept >= 0 ? kept : (long?)null;
                _references.TryGetValue(garageEvent.Id, out var reference);
                // The server's Pull-out first: the office may have corrected what was typed here.
                warning = OdometerRules.Warning(isPullIn, reading, reference, reference?.PullOutReading ?? onPhone);
            }
            catch (Exception ex)
            {
                // A check that fails must never stand between the driver and the save.
                Debug.WriteLine($"Odometer check: {ex.Message}");
                return true;
            }

            return warning is null || await Shell.Current.DisplayAlert("Check the odometer",
                warning + "\n\nIf the dashboard really shows that number, save it.", "Save", "Change it");
        }

        public void RememberPullOut(ScheduleDto garageEvent, long reading)
        {
            if (garageEvent.Name != "Pull-in") Preferences.Set(Key(garageEvent), reading);
        }

        private static string Key(ScheduleDto e) => $"PullOutOdometer:{e.VehicleRouteId}:{e.Date:yyyyMMdd}";
    }
}
