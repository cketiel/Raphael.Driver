
using Raphael.Driver.Models;

namespace Raphael.Driver.DTOs
{
    public class ScheduleDto
    {
        public int Id { get; set; }
        public int? TripId { get; set; }
        public string Name { get; set; }
        public TimeSpan? Pickup { get; set; } // Mapped from ScheduledPickupTime
        public TimeSpan? Appt { get; set; }   // Mapped from ScheduledApptTime
        public TimeSpan? ETA { get; set; }
        public double? Distance { get; set; }
        public TimeSpan? Travel { get; set; }

        /// <summary>
        /// How long this pickup leaves the driver waiting for the hour to come round, or null
        /// when there is no wait.
        /// </summary>
        /// <remarks>
        /// Derived and written by the server. Nothing in this app draws it yet; it is here so the
        /// copy stays in step with <c>Raphael.Shared/DTOs/ScheduleDto.cs</c>, which is the source
        /// of truth, and so a driver's screen can show it without a round of contract work first.
        /// </remarks>
        public TimeSpan? Wait { get; set; }
        public int? On { get; set; }
        public string Address { get; set; }
        public double ScheduleLatitude { get; set; }
        public double ScheduleLongitude { get; set; }
        public string? Comment { get; set; }
        public string? Phone { get; set; }
        public TimeSpan? Arrive { get; set; }
        public TimeSpan? Perform { get; set; }
        public double? ArriveDist { get; set; }
        public double? PerformDist { get; set; }
        public string? Driver { get; set; }
        public string? GPSArrive { get; set; }
        public long? Odometer { get; set; }
        public string? AuthNo { get; set; }
        public string? FundingSource { get; set; }
        public DateTime? Date { get; set; }
        public int? Sequence { get; set; }
        public ScheduleEventType? EventType { get; set; }

        public string? SpaceType { get; set; }
        public string? TripType { get; set; }
        public string? Patient { get; set; }
        public bool Performed { get; set; }
        public string? Run { get; set; }
        public string? Vehicle { get; set; }
        public int VehicleRouteId { get; set; }
        public byte[]? PassengerSignature { get; set; }

        // To update the member's phone number
        public int? CustomerId { get; set; }
        public string? CustomerPhone { get; set; }
    }
}
