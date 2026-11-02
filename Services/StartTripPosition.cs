namespace Raphael.Driver.Services
{
    /// <summary>
    /// Body of <c>POST api/Trips/{id}/start</c> (RE-062): where the vehicle is and when the driver
    /// pressed Start. Copy of <c>Raphael.Shared/DTOs/StartTripRequestDto.cs</c>.
    /// </summary>
    public class StartTripPosition
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? StartedAtUtc { get; set; }
    }
}
