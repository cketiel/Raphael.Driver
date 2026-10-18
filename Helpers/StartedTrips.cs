using System.Text.Json;
using Microsoft.Maui.Storage;

namespace Raphael.Driver.Helpers
{
    /// <summary>
    /// The trips this driver started today, kept on the phone (RE-057).
    /// </summary>
    /// <remarks>
    /// ⚠️ This is what keeps Start from ever blocking a driver. The server may not have heard about
    /// the start yet (no signal, a server error): the trip still counts as started here, so Arrive is
    /// offered, and the request is retried. It also survives the app being closed and reopened.
    ///
    /// <para>
    /// One key for the whole day, not one per trip: it is emptied when the day changes, so it never
    /// grows. <see cref="Pending"/> lists the starts the server has not confirmed yet.
    /// </para>
    /// </remarks>
    public static class StartedTrips
    {
        private const string Key = "StartedTrips";

        private sealed class Day
        {
            public string Date { get; set; } = "";
            public List<int> Started { get; set; } = new();
            public List<int> Pending { get; set; } = new();
        }

        private static readonly object Gate = new();

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        private static Day Load()
        {
            try
            {
                var json = Preferences.Get(Key, string.Empty);
                var day = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Day>(json);
                return day != null && day.Date == Today ? day : new Day { Date = Today };
            }
            catch (JsonException)
            {
                return new Day { Date = Today };
            }
        }

        private static void Save(Day day) => Preferences.Set(Key, JsonSerializer.Serialize(day));

        public static bool IsStarted(int tripId)
        {
            lock (Gate) return Load().Started.Contains(tripId);
        }

        /// <summary>The driver pressed Start Trip; the server is told separately.</summary>
        public static void MarkStarted(int tripId)
        {
            lock (Gate)
            {
                var day = Load();
                if (!day.Started.Contains(tripId)) day.Started.Add(tripId);
                if (!day.Pending.Contains(tripId)) day.Pending.Add(tripId);
                Save(day);
            }
        }

        /// <summary>The server answered: nothing left to retry for this trip.</summary>
        public static void MarkConfirmed(int tripId)
        {
            lock (Gate)
            {
                var day = Load();
                if (day.Pending.Remove(tripId)) Save(day);
            }
        }

        public static IReadOnlyList<int> Pending
        {
            get { lock (Gate) return Load().Pending.ToList(); }
        }

        private static int _retrying;

        /// <summary>
        /// Sends again the starts the server did not get (no signal at the time). Called when the
        /// schedule loads, so reopening the app catches up, and when a stop is opened.
        /// </summary>
        /// <param name="serverStatuses">
        /// The trip statuses the server just sent, when known: a trip it already shows as started
        /// is confirmed without sending anything.
        /// </param>
        public static async Task RetryPendingAsync(
            Services.IScheduleService schedules,
            IReadOnlyDictionary<int, string?>? serverStatuses = null)
        {
            if (Interlocked.Exchange(ref _retrying, 1) == 1) return;
            try
            {
                foreach (var tripId in Pending)
                {
                    if (serverStatuses != null
                        && serverStatuses.TryGetValue(tripId, out var status)
                        && (status == "Started" || status == "Arrived" || status == "InProgress"))
                    {
                        MarkConfirmed(tripId);
                        continue;
                    }

                    var outcome = await schedules.StartTripAsync(tripId, null);
                    if (outcome != Services.StartTripOutcome.Failed) MarkConfirmed(tripId);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _retrying, 0);
            }
        }
    }
}
