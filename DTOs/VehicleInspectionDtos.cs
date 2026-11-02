namespace Raphael.Driver.DTOs
{
    // RE-062. Manual copies of Raphael.Shared/DTOs/Inspections/VehicleInspectionDtos.cs, the source of
    // truth: only the types and fields the driver app uses.

    public class VehicleInspectionRequirementDto
    {
        public int ScheduleId { get; set; }

        /// <summary><c>PullOut</c> or <c>PullIn</c>.</summary>
        public string Kind { get; set; } = string.Empty;

        public bool Required { get; set; }

        public int ChecklistVersion { get; set; }

        public string? VehicleName { get; set; }

        public string? VinOnFile { get; set; }

        public List<VehicleInspectionChecklistItemDto> Items { get; set; } = new();

        public VehicleInspectionResultDto? Existing { get; set; }
    }

    public class VehicleInspectionChecklistItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
    }

    public class VehicleInspectionCreateDto
    {
        public Guid ClientId { get; set; }
        public int ScheduleId { get; set; }
        public int ChecklistVersion { get; set; }
        public long Odometer { get; set; }
        public bool VinConfirmed { get; set; }
        public string? VinReported { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<VehicleInspectionAnswerDto> Items { get; set; } = new();
    }

    public class VehicleInspectionAnswerDto
    {
        public string Code { get; set; } = string.Empty;
        public bool IsProblem { get; set; }
    }

    public class VehicleInspectionResultDto
    {
        public int Id { get; set; }
        public Guid ClientId { get; set; }
        public long Odometer { get; set; }
        public int ProblemCount { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public List<string> PhotosPending { get; set; } = new();
        public bool SignaturePending { get; set; }
    }
}
