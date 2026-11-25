using System.Globalization;

namespace Raphael.Driver.Helpers
{
    /// <summary>Copy of the server's <c>OdometerReferenceDto</c>: what a reading is compared with before saving (RE-092).</summary>
    public sealed class OdometerReference
    {
        public int ScheduleId { get; set; }
        public string Event { get; set; } = string.Empty;
        public long? LastReading { get; set; }
        public DateTime? LastReadingDate { get; set; }

        /// <summary>At a Pull-in: the same route-day's Pull-out.</summary>
        public long? PullOutReading { get; set; }
        public double? GpsMiles { get; set; }
        public double? PlannedMiles { get; set; }

        /// <summary>Null when the office turned that warning off.</summary>
        public double? PullInTimesLimit { get; set; }
        public double? PullOutMilesLimit { get; set; }
        public bool WarnWhenLower { get; set; }
    }

    /// <summary>
    /// Whether an odometer reading deserves a second look (RE-092). Pure: no network, no screen. It only
    /// warns: whatever it says, the driver can save.
    /// </summary>
    public static class OdometerRules
    {
        /// <summary>As the server: a small gap is never "impossible", whatever the proportion.</summary>
        public const double ImpossibleAtLeastMiles = 25;

        /// <param name="reference">Null without coverage, or when the server does not have it.</param>
        /// <param name="pullOutOnPhone">This day's Pull-out reading, when it was typed on this phone.</param>
        /// <returns>What to tell the driver, or null when the reading makes sense.</returns>
        public static string? Warning(bool isPullIn, long reading, OdometerReference? reference, long? pullOutOnPhone)
        {
            static string N(double value) => Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture);

            if (!isPullIn)
            {
                if (reference?.LastReading is not long last) return null;
                var on = reference.LastReadingDate is DateTime d ? $" ({d.ToString("MMM d", CultureInfo.InvariantCulture)})" : "";
                if (reading < last)
                    return reference.WarnWhenLower ? $"You typed {N(reading)}, which is lower than this vehicle's last reading: {N(last)}{on}." : null;
                return reference.PullOutMilesLimit is double limit && reading - last > limit
                    ? $"You typed {N(reading)}. That is {N(reading - last)} miles more than this vehicle's last reading: {N(last)}{on}. Check for an extra or a wrong digit."
                    : null;
            }

            if ((pullOutOnPhone ?? reference?.PullOutReading) is not long start) return null;
            if (reading < start)
                return reference?.WarnWhenLower ?? true ? $"You typed {N(reading)}, which is lower than the Pull-out reading: {N(start)}." : null;

            var miles = reading - start;
            double? expected = reference?.GpsMiles is > 1 ? reference.GpsMiles : reference?.PlannedMiles is > 1 ? reference.PlannedMiles : null;
            return expected is double e && reference?.PullInTimesLimit is double times && miles > e * times && miles - e > ImpossibleAtLeastMiles
                ? $"You typed {N(reading)}. That is {N(miles)} miles since the Pull-out ({N(start)}), and the route was about {N(e)} miles. Check for an extra or a wrong digit."
                : null;
        }
    }
}
