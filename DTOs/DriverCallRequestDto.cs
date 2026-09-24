namespace Raphael.Driver.DTOs
{
    // Mirror of DriverCallRequestDto and CreateCallRequestDto in
    // Raphael.Shared/DTOs/CallRequests/CallRequestDtos.cs, copied by hand like every DTO here.
    // Only the two this app uses: the office's side of the queue never reaches a phone.

    /// <summary>What the "Request call" button needs to draw itself.</summary>
    public class DriverCallRequestDto
    {
        public bool HasOpenRequest { get; set; }

        public int? Id { get; set; }

        /// <summary>Waiting or InProgress while open. Text on the wire, not an enum number.</summary>
        public string? Status { get; set; }

        public DateTime? RequestedAtUtc { get; set; }

        public int ReminderCount { get; set; }

        public DateTime? LastReminderAtUtc { get; set; }

        public string? ClaimedByFirstName { get; set; }

        public DateTime? ClaimedAtUtc { get; set; }

        public int CallAttempts { get; set; }

        public DateTime? LastAttemptAtUtc { get; set; }

        /// <summary>The office tried to call and the driver has not said they can talk since.</summary>
        public bool MissedCallPending { get; set; }

        /// <summary>Null when the driver may press again right now.</summary>
        public DateTime? NextSignalAllowedAtUtc { get; set; }

        /// <summary>False when the press came inside the throttle window and changed nothing.</summary>
        public bool SignalAccepted { get; set; }

        /// <summary>The number the office will call. Empty means the office has none on file.</summary>
        public string? CallbackPhone { get; set; }

        /// <summary>The server's clock, so the countdown does not depend on the phone's.</summary>
        public DateTime ServerTimeUtc { get; set; }

        public bool IsInProgress => Status == "InProgress";
    }

    public class CreateCallRequestDto
    {
        public int? VehicleRouteId { get; set; }

        public int? ScheduleId { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}
