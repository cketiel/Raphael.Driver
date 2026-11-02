using System.Diagnostics;
using System.Text.Json;
using Raphael.Driver.DTOs;
using Raphael.Driver.Services.Offline;

namespace Raphael.Driver.Services
{
    /// <summary>A finished inspection, with its evidence, ready to go to the server.</summary>
    public sealed class FinishedInspection
    {
        public VehicleInspectionCreateDto Inspection { get; init; } = new();

        /// <summary>JPEG per checklist code marked as a problem (and the VIN when it was not the one on file).</summary>
        public Dictionary<string, byte[]> Photos { get; init; } = new();

        public byte[] Signature { get; init; } = Array.Empty<byte>();
    }

    public interface IInspectionService
    {
        /// <summary>
        /// What this Pull-out or Pull-in asks of the driver. From the server, or from the phone's copy
        /// without coverage; with neither, the inspection is asked for (see remarks of the class).
        /// </summary>
        Task<VehicleInspectionRequirementDto> GetRequirementAsync(int scheduleId, string eventName);

        /// <summary>Fetches and keeps the requirement while there is coverage. Never throws.</summary>
        Task PrefetchAsync(IEnumerable<(int ScheduleId, string EventName)> garageEvents);

        /// <summary>Sends the inspection, its photos and the signature through the outbox, in that order.</summary>
        Task<OutboxResult> SubmitAsync(FinishedInspection finished);

        /// <summary>Whether this phone already inspected this Pull-out or Pull-in (sent or still waiting).</summary>
        bool IsDoneOnPhone(int scheduleId);
    }

    /// <summary>
    /// The vehicle inspection from the driver's side (RE-062).
    /// </summary>
    /// <remarks>
    /// ⚠️ Without coverage and without a copy of what the server said, the inspection is asked for
    /// anyway: guessing "not required" when the Provider does require it would make the Pull-out the
    /// server later refuses, and the route would not start. An inspection the Provider did not need is
    /// accepted all the same, so the safe guess costs the driver a few minutes at most.
    /// </remarks>
    public sealed class InspectionService : IInspectionService
    {
        private const string DoneKey = "InspectionsDoneOnPhone";
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        /// <summary>The checklist the server serves, version 1, for the case with no copy at all.</summary>
        private static readonly (string Code, string Label, string Group)[] FallbackChecklist =
        {
            ("vin", "VIN matches the vehicle", "Vehicle"),
            ("body.left", "Driver side clean, no dents or damage", "Body"),
            ("body.right", "Passenger side clean, no dents or damage", "Body"),
            ("body.front", "Front end clean, no dents or damage", "Body"),
            ("body.rear", "Rear end clean, no dents or damage", "Body"),
            ("lights.headlights", "Headlights (low and high beam) working", "Lights"),
            ("lights.turn_right", "Right turn signal working", "Lights"),
            ("lights.turn_left", "Left turn signal working", "Lights"),
            ("lights.brake", "Brake lights working", "Lights"),
            ("fluid.coolant", "Coolant level OK", "Fluids"),
            ("fluid.brake", "Brake fluid level OK", "Fluids"),
            ("fluid.oil", "Engine oil level OK", "Fluids"),
            ("tires", "All 4 tires in good condition", "Tires and fuel"),
            ("fuel", "Fuel tank above 80%", "Tires and fuel"),
            ("clean.outside", "Exterior clean", "Cleanliness"),
            ("clean.inside", "Interior clean", "Cleanliness"),
            ("wheelchair.hooks", "All 4 wheelchair tie-downs in place (wheelchair vehicles)", "Equipment"),
            ("seatbelt.chest", "Shoulder belt working", "Equipment"),
            ("emergency.kit", "Jack, spare tire and jumper cables on board", "Equipment"),
            ("ac", "A/C working", "Equipment"),
        };

        private readonly IHttpClientFactory _factory;
        private readonly Outbox _outbox;
        private readonly string _folder = Path.Combine(FileSystem.AppDataDirectory, "cache", "inspections");

        public InspectionService(IHttpClientFactory factory, Outbox outbox)
        {
            _factory = factory;
            _outbox = outbox;
            Directory.CreateDirectory(_folder);
        }

        public async Task<VehicleInspectionRequirementDto> GetRequirementAsync(int scheduleId, string eventName)
        {
            // ⚠️ Never throws: a failure here would fall back to the old flow, which the server
            // refuses when the Provider requires the inspection.
            try
            {
                Directory.CreateDirectory(_folder);

                var fetched = await FetchAsync(scheduleId);
                if (fetched is not null) return fetched;

                var file = Path.Combine(_folder, $"{scheduleId}.json");
                if (TryRead(file) is { } copy) return copy;

                // Another garage event of the same day or route: same Provider, same vehicle, same list.
                var latest = Directory.GetFiles(_folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (latest is not null && TryRead(latest) is { } sibling)
                {
                    sibling.ScheduleId = scheduleId;
                    sibling.Kind = KindOf(eventName);
                    sibling.Existing = null;
                    return sibling;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Inspection requirement: {ex.Message}");
            }

            return new VehicleInspectionRequirementDto
            {
                ScheduleId = scheduleId,
                Kind = KindOf(eventName),
                Required = true,
                ChecklistVersion = 1,
                Items = FallbackChecklist.Select(i => new VehicleInspectionChecklistItemDto { Code = i.Code, Label = i.Label, Group = i.Group }).ToList()
            };
        }

        public async Task PrefetchAsync(IEnumerable<(int ScheduleId, string EventName)> garageEvents)
        {
            // Once per garage event: the copy is what matters without coverage, and asking again at
            // every reload of the schedule would be a request per reload for nothing.
            Directory.CreateDirectory(_folder);
            foreach (var (id, _) in garageEvents.Where(e => !File.Exists(Path.Combine(_folder, $"{e.ScheduleId}.json"))))
            {
                try { await FetchAsync(id); }
                catch (Exception ex) { Debug.WriteLine($"Inspection prefetch: {ex.Message}"); }
            }
        }

        public async Task<OutboxResult> SubmitAsync(FinishedInspection finished)
        {
            var dto = finished.Inspection;
            var first = await _outbox.SendAsync(OutboxRequest.Json("POST", "api/vehicle-inspections", dto, "inspection", dto.ScheduleId));

            // Refused outright: nothing else of it is sent. "Already inspected" counts as done.
            if (first.Outcome == OutboxOutcome.Rejected && first.Status != 409) return first;

            MarkDone(dto.ScheduleId);
            if (first.Outcome == OutboxOutcome.Rejected) return new OutboxResult(OutboxOutcome.Sent, 409);

            foreach (var (code, jpeg) in finished.Photos)
                await _outbox.SendAsync(OutboxRequest.Bytes("PUT", $"api/vehicle-inspections/{dto.ClientId}/items/{Uri.EscapeDataString(code)}/photo", jpeg, "image/jpeg", "inspection-photo"));

            if (finished.Signature.Length > 0)
                await _outbox.SendAsync(OutboxRequest.Bytes("PUT", $"api/vehicle-inspections/{dto.ClientId}/signature", finished.Signature, "image/png", "inspection-signature"));

            return first;
        }

        public bool IsDoneOnPhone(int scheduleId) => DoneIds().Contains(scheduleId);

        private async Task<VehicleInspectionRequirementDto?> FetchAsync(int scheduleId)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;
            try
            {
                var http = _factory.CreateClient(nameof(Outbox));
                var response = await http.GetAsync($"api/vehicle-inspections/requirement?scheduleId={scheduleId}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var requirement = JsonSerializer.Deserialize<VehicleInspectionRequirementDto>(content, Json);
                if (requirement is null) return null;

                Directory.CreateDirectory(_folder);
                File.WriteAllText(Path.Combine(_folder, $"{scheduleId}.json"), content);
                foreach (var old in Directory.GetFiles(_folder, "*.json").Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-3)))
                    File.Delete(old);
                if (requirement.Existing is not null) MarkDone(scheduleId);
                return requirement;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
            {
                Debug.WriteLine($"Inspection requirement from the phone's copy: {ex.Message}");
                return null;
            }
        }

        private static VehicleInspectionRequirementDto? TryRead(string file)
        {
            try
            {
                return File.Exists(file) ? JsonSerializer.Deserialize<VehicleInspectionRequirementDto>(File.ReadAllText(file), Json) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string KindOf(string eventName) => eventName == "Pull-in" ? "PullIn" : "PullOut";

        private static HashSet<int> DoneIds()
        {
            try
            {
                return JsonSerializer.Deserialize<HashSet<int>>(Preferences.Get(DoneKey, "[]")) ?? new HashSet<int>();
            }
            catch (JsonException)
            {
                return new HashSet<int>();
            }
        }

        private static void MarkDone(int scheduleId)
        {
            var ids = DoneIds();
            if (ids.Add(scheduleId))
            {
                // Garage events of the last days only: ids only grow, so keep the newest hundred.
                var keep = ids.OrderByDescending(i => i).Take(100).ToHashSet();
                Preferences.Set(DoneKey, JsonSerializer.Serialize(keep));
            }
        }
    }
}
