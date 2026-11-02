

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;
using Raphael.Driver.Services.Offline;

namespace Raphael.Driver.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly Outbox _outbox;

        public ScheduleService(HttpClient httpClient, Outbox outbox)
        {
            _outbox = outbox;
            // The base URL of your API. It should be in a centralized place, like Preferences or a config file.
            // https://krasnovbw-001-site1.rtempurl.com/
            // https://localhost:7244/
            /*var baseUrl = Preferences.Get("ApiBaseUrl", "https://krasnovbw-001-site1.rtempurl.com/");
            baseUrl = "https://krasnovbw-001-site1.rtempurl.com/";*/

            //_httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _httpClient = httpClient; // This httpClient already includes the BaseAddress and the Token
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Important to match property names
            };
        }

        public Task<List<ScheduleDto>> GetSchedulesByRunAsync(string runLogin, DateTime date) =>
            GetDayAsync("api/Schedules/by-run-login", "all", runLogin, date, pendingOnly: false);

        public Task<List<ScheduleDto>> GetPendingSchedulesByRunAsync(string runLogin, DateTime date) =>
            GetDayAsync("api/Schedules/driver/pending", "pending", runLogin, date, pendingOnly: true);

        /// <summary>
        /// The day's stops, from the server when it answers and from the phone's last copy when it
        /// does not (RE-062), with the changes still waiting to be sent laid on top.
        /// </summary>
        /// <remarks>
        /// ⚠️ The overlay is what keeps a Perform done without coverage from coming back as pending at
        /// the next reload: the server does not know it yet, the phone does. The copy lives in the app's
        /// own storage, like the screen it feeds, and only the last two days are kept.
        /// </remarks>
        private async Task<List<ScheduleDto>> GetDayAsync(string endpoint, string kind, string runLogin, DateTime date, bool pendingOnly)
        {
            if (string.IsNullOrWhiteSpace(runLogin))
                return new List<ScheduleDto>();

            var requestUri = $"{endpoint}?runLogin={HttpUtility.UrlEncode(runLogin)}&date={date:yyyy-MM-dd}";
            var cache = CacheFile(kind, runLogin, date);
            List<ScheduleDto>? schedules = null;

            try
            {
                var response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    schedules = JsonSerializer.Deserialize<List<ScheduleDto>>(content, _serializerOptions) ?? new List<ScheduleDto>();
                    TryWrite(cache, content);

                    // RE-062: while there is coverage, the phone learns what each garage event will ask.
                    var garage = schedules.Where(s => s.Name is "Pull-out" or "Pull-in").Select(s => (s.Id, s.Name)).ToList();
                    if (garage.Count > 0) _ = ServiceHelper.GetService<IInspectionService>()?.PrefetchAsync(garage);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    // RE-063: another company's route. Its copy goes too, or the phone would keep
                    // showing it "without coverage".
                    TryDelete(cache);
                    throw new RouteNotAllowedException();
                }
                else
                {
                    Debug.WriteLine($"Error fetching schedule: {response.StatusCode}");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                Debug.WriteLine($"Schedule from the phone's copy: {ex.Message}");
            }

            if (schedules is null)
            {
                if (!File.Exists(cache)) return new List<ScheduleDto>();
                schedules = JsonSerializer.Deserialize<List<ScheduleDto>>(await File.ReadAllTextAsync(cache), _serializerOptions) ?? new List<ScheduleDto>();
            }

            // What the driver did and the server has not heard yet: the last saved state of each row wins.
            foreach (var group in _outbox.Pending<ScheduleDto>(ScheduleKind).GroupBy(p => p.Body.Id))
            {
                var latest = group.Last().Body;
                var index = schedules.FindIndex(s => s.Id == latest.Id);
                if (index < 0) continue;
                if (pendingOnly && latest.Performed) schedules.RemoveAt(index);
                else schedules[index] = latest;
            }

            return schedules;
        }

        private const string ScheduleKind = "schedule";

        private static string CacheFile(string kind, string runLogin, DateTime date)
        {
            var folder = Path.Combine(FileSystem.AppDataDirectory, "cache");
            Directory.CreateDirectory(folder);
            var safe = string.Concat(runLogin.Where(char.IsLetterOrDigit));
            return Path.Combine(folder, $"schedules-{kind}-{safe}-{date:yyyyMMdd}.json");
        }

        private static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (IOException ex) { Debug.WriteLine($"Could not delete {file}: {ex.Message}"); }
        }

        private static void TryWrite(string file, string content)
        {
            try
            {
                File.WriteAllText(file, content);
                // Only the last two days' copies stay on the phone.
                foreach (var old in Directory.GetFiles(Path.GetDirectoryName(file)!, "schedules-*.json")
                             .Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-2)))
                    File.Delete(old);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not keep the schedule on the phone: {ex.Message}");
            }
        }

        public async Task<bool> UpdateScheduleAsync(ScheduleDto scheduleToUpdate) =>
            scheduleToUpdate != null && (await SaveScheduleAsync(scheduleToUpdate)).Accepted;

        /// <summary>
        /// Saves a stop through the outbox (RE-062): sent now, or kept and sent when the coverage
        /// comes back. A refusal carries the server's status and answer, e.g. 409 inspection_required.
        /// </summary>
        public Task<OutboxResult> SaveScheduleAsync(ScheduleDto schedule) =>
            _outbox.SendAsync(OutboxRequest.Json("PUT", $"api/Schedules/{schedule.Id}", schedule, ScheduleKind, schedule.Id));

        public async Task<bool> PerformScheduleUpdateAsync(ScheduleDto scheduleToUpdate)
        {
            if (scheduleToUpdate == null)
                return false;

            var result = await _outbox.SendAsync(OutboxRequest.Json("PUT", $"api/Schedules/{scheduleToUpdate.Id}/perform", scheduleToUpdate, ScheduleKind, scheduleToUpdate.Id));
            if (result.Outcome == OutboxOutcome.Rejected)
                Debug.WriteLine($"Error in PerformScheduleUpdateAsync: {result.Status}");
            return result.Accepted;
        }

        public Task<List<ScheduleDto>> GetTodayScheduleAsync()
        {

            // Por ahora, usar datos de prueba.

            var mockSchedules = new List<ScheduleDto>
            {
                // Caso 1: Pickup Appointment (Verde)
                new ScheduleDto {
                    Id = 1, SpaceType = "AMB", FundingSource = "PRIVADO", ETA = new TimeSpan(12, 0, 0),
                    EventType = ScheduleEventType.Pickup, TripType = "Appointment", Patient = "test, system",
                    Pickup = new TimeSpan(12, 0, 0), Address = "405 SE VAN LOON TERRACE, CAPE CORAL, FL, 33990"
                },
                // Caso 2: Dropoff Appointment (Rojo)
                new ScheduleDto {
                    Id = 2, SpaceType = "AMB", FundingSource = "PRIVADO", ETA = new TimeSpan(12, 30, 0),
                    EventType = ScheduleEventType.Dropoff, TripType = "Appointment", Patient = "test, system",
                    Appt = new TimeSpan(12, 30, 0), Address = "525 SE VAN LOON TERRACE, CAPE CORAL, FL, 33990"
                },
                 // Caso 3: Pickup Return (Azul)
                new ScheduleDto {
                    Id = 3, SpaceType = "WCH", FundingSource = "MEDICAID", ETA = new TimeSpan(14, 0, 0),
                    EventType = ScheduleEventType.Pickup, TripType = "Return", Patient = "Doe, Jane",
                    Pickup = new TimeSpan(14, 0, 0), Address = "101 PINE AVE, MIAMI, FL, 33101"
                },
                // Caso 4: Dropoff Return (Morado)
                new ScheduleDto {
                    Id = 4, SpaceType = "WCH", FundingSource = "MEDICAID", ETA = new TimeSpan(14, 45, 0),
                    EventType = ScheduleEventType.Dropoff, TripType = "Return", Patient = "Doe, Jane",
                    Appt = new TimeSpan(14, 45, 0), Address = "202 OAK ST, MIAMI, FL, 33101"
                },
                // Caso 5: Pull-in (Negro)
                new ScheduleDto {
                    Id = 5, Name = "Pull-in", SpaceType = "VEHICLE", FundingSource = "INTERNAL", ETA = new TimeSpan(17, 0, 0),
                    EventType = null, TripType = "Internal", Patient = "Driver Return",
                    Pickup = new TimeSpan(17, 0, 0), Address = "COMPANY BASE, NAPLES, FL, 34101"
                }
            };

            return Task.FromResult(mockSchedules);
        }

        public async Task<bool> SaveSignatureAsync(int scheduleId, string signatureBase64)
        {
            var uploadDto = new SignatureUploadDto { SignatureBase64 = signatureBase64 };
            var result = await _outbox.SendAsync(OutboxRequest.Json("POST", $"api/Schedules/{scheduleId}/signature", uploadDto, "signature", scheduleId));
            if (result.Outcome == OutboxOutcome.Rejected)
                Debug.WriteLine($"Error saving signature: {result.Status}");
            return result.Accepted;
        }

        public async Task<byte[]?> GetSignatureAsync(int scheduleId)
        {
            var requestUri = $"api/Schedules/{scheduleId}/signature";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    // The backend returns the file bytes directly
                    return await response.Content.ReadAsByteArrayAsync();
                }

                // If the response is 404 (Not Found), it means there is no signature, we return null
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"Error getting signature: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetSignatureAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<StartTripOutcome> StartTripAsync(int tripId, TimeSpan? travel, StartTripPosition? position = null)
        {
            // The travel time the route already carries: the patient is told how long, and nothing
            // is asked of Google for it. With the vehicle's position (RE-062) the server re-times the
            // pickup from where the vehicle really is; the position travels in the body, never in the
            // address.
            var requestUri = travel.HasValue
                ? $"api/Trips/{tripId}/start?travel={HttpUtility.UrlEncode(travel.Value.ToString(@"hh\:mm\:ss"))}"
                : $"api/Trips/{tripId}/start";

            var request = position is null
                ? OutboxRequest.Empty("POST", requestUri, "start")
                : OutboxRequest.Json("POST", requestUri, position, "start");

            var result = await _outbox.SendAsync(request);

            // Kept for later counts as told: the outbox delivers it, with the moment it was pressed.
            return result.Outcome == OutboxOutcome.Rejected ? StartTripOutcome.Refused : StartTripOutcome.Started;
        }

        public async Task<bool> CancelTripByDriverAsync(int tripId, string reason)
        {
            var uploadDto = new DriverCancelTripDto { Reason = reason };
            return (await _outbox.SendAsync(OutboxRequest.Json("POST", $"api/Trips/{tripId}/cancel-by-driver", uploadDto, "cancel"))).Accepted;
        }

        /// <summary>
        /// Tomorrow's events for this run. What the Future Schedule button shows.
        /// </summary>
        /// <remarks>
        /// The button keeps the name the driver has always pressed; what it shows is the next
        /// day. Everything ahead came back as one list with several Pull-outs and several
        /// Pull-ins in it, and nothing told the driver which day a row belonged to.
        ///
        /// <para>
        /// The API still serves <c>driver/future</c> under that name, because that is what it
        /// does. This asks for <c>driver/next-day</c>.
        /// </para>
        /// </remarks>
        public async Task<List<ScheduleDto>> GetNextDaySchedulesByRunAsync(string runLogin)
        {
            try
            {
                var encodedRunLogin = HttpUtility.UrlEncode(runLogin);
                var response = await _httpClient.GetAsync($"api/Schedules/driver/next-day?runLogin={encodedRunLogin}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<ScheduleDto>>();
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    throw new RouteNotAllowedException();
            }
            catch (Exception ex) when (ex is not RouteNotAllowedException)
            {
                Debug.WriteLine($"Error fetching next day schedules: {ex.Message}");
            }
            return new List<ScheduleDto>();
        }

        public async Task<List<ScheduleHistoryDto>> GetScheduleHistoryAsync(string runLogin, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(runLogin))
                return new List<ScheduleHistoryDto>();

            var dateString = date.ToString("yyyy-MM-dd");
            //dateString = "2025-09-15"; // esto es para probar
            var encodedRunLogin = HttpUtility.UrlEncode(runLogin);

            
            //var response = await _httpClient.GetAsync($"api/schedules/history/{runLogin}/{dateString}");


            var requestUri = $"api/schedules/history/{encodedRunLogin}/{dateString}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var schedules = JsonSerializer.Deserialize<List<ScheduleHistoryDto>>(content, _serializerOptions);
                    return schedules ?? new List<ScheduleHistoryDto>();
                }
                else
                {
                    Debug.WriteLine($"Error fetching schedule history: {response.StatusCode} from {requestUri}");
                    return new List<ScheduleHistoryDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetScheduleHistoryAsync: {ex.Message}");
                throw;

            }

        }

        public async Task<int> GetScheduleHistoryCountAsync(string runLogin, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(runLogin))
                return 0;

            var dateString = date.ToString("yyyy-MM-dd");
            //dateString = "2025-09-15"; // esto es para probar
            var encodedRunLogin = HttpUtility.UrlEncode(runLogin);

            //var response = await _httpClient.GetAsync($"api/schedules/history/count/{runLogin}/{dateString}");

            var requestUri = $"api/schedules/history/count/{encodedRunLogin}/{dateString}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out int count))
                    {
                        return count;
                    }
                    else
                    {
                        Debug.WriteLine($"Could not parse history count response: '{content}'");
                        return 0; 
                    }
                }
                else
                {
                    Debug.WriteLine($"Error fetching history count: {response.StatusCode} from {requestUri}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetScheduleHistoryCountAsync: {ex.Message}");
                throw;

            }
          
        }

        public async Task<bool> UpdateContactPhoneNumberAsync(int tripId, string newPhoneNumber)
        {
            var updatePayload = new { PhoneNumber = newPhoneNumber };
            return (await _outbox.SendAsync(OutboxRequest.Json("PUT", $"api/Schedules/trip/{tripId}/contact-phone", updatePayload, "phone"))).Accepted;
        }

        public async Task UpdateNextSchedulesETAsAsync(List<ScheduleDto> allPendingEvents, int currentEventId, TimeSpan actualPerformTime)
        {
            // Find the index of the current event
            int currentIndex = allPendingEvents.FindIndex(e => e.Id == currentEventId);
            if (currentIndex == -1) return;

            // The next two stops, and the legs that reach them. Both legs are asked for in one
            // request: they used to be two round trips to Google, made one after the other while
            // the driver's screen waited, on every event confirmed all day.
            var upcoming = new List<ScheduleDto>();
            var legs = new List<RouteLegRequestItemDto>();

            double lastLat = allPendingEvents[currentIndex].ScheduleLatitude;
            double lastLng = allPendingEvents[currentIndex].ScheduleLongitude;
            TimeSpan departure = actualPerformTime;

            for (int i = currentIndex + 1; i <= currentIndex + 2 && i < allPendingEvents.Count; i++)
            {
                var nextEvent = allPendingEvents[i];

                upcoming.Add(nextEvent);

                legs.Add(new RouteLegRequestItemDto
                {
                    OriginLat = lastLat,
                    OriginLng = lastLng,
                    DestLat = nextEvent.ScheduleLatitude,
                    DestLng = nextEvent.ScheduleLongitude,
                    Date = nextEvent.Date ?? DateTime.Today,

                    // The hour the vehicle leaves for this leg. Exact for the first — the driver
                    // just performed the stop — and the scheduled hour for the second, which is
                    // close enough for an hour-wide traffic bucket.
                    DepartureTime = i == currentIndex + 1 ? actualPerformTime : nextEvent.Pickup
                });

                lastLat = nextEvent.ScheduleLatitude;
                lastLng = nextEvent.ScheduleLongitude;
            }

            if (upcoming.Count == 0) return;

            var results = await GetRouteLegsAsync(legs);

            for (int i = 0; i < upcoming.Count; i++)
            {
                var leg = i < results.Count ? results[i] : null;

                // ⚠️ A leg nobody could price leaves the ETA as it was. It also breaks the chain:
                // the stop after it would otherwise be timed from an arrival that was never
                // calculated.
                if (leg == null || !leg.IsUsable) break;

                var nextEvent = upcoming[i];

                TimeSpan travelTime = leg.TravelTime;

                // Calculate new ETA: Previous departure time + Travel time
                TimeSpan newEta = ApplyEarlyArrivalLimit(departure.Add(travelTime), nextEvent);

                // Update object locally
                nextEvent.ETA = newEta;
                nextEvent.Travel = travelTime;

                // Send update to Backend
                await UpdateETAAsync(nextEvent);

                // For the following stop, the starting point is this one. The capped time, not
                // the raw one: chaining from a moment the driver will not actually leave puts
                // every ETA after it ahead of reality.
                departure = newEta;
            }
        }

        /// <summary>
        /// Prices legs through Raphael.Api, which answers from a cache shared with the dispatch
        /// office and buys from Google only what nobody has asked for yet.
        /// </summary>
        /// <remarks>
        /// The driver app no longer holds a Google key. It used to carry one hardcoded in
        /// <c>PrivateSettings</c>, which meant it travelled inside every distributed APK.
        /// </remarks>
        private async Task<List<RouteLegResultDto>> GetRouteLegsAsync(List<RouteLegRequestItemDto> legs)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/routing/legs",
                    new RouteLegsRequestDto { Legs = legs });

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Routing request failed: {response.StatusCode}");

                    return new List<RouteLegResultDto>();
                }

                var content = await response.Content.ReadAsStringAsync();

                var payload = JsonSerializer.Deserialize<RouteLegsResponseDto>(content, _serializerOptions);

                return payload?.Legs ?? new List<RouteLegResultDto>();
            }
            catch (Exception ex)
            {
                // A driver in a dead spot keeps the ETAs already on screen. Throwing here would
                // take down the confirmation of an event that already happened.
                Debug.WriteLine($"Exception in GetRouteLegsAsync: {ex.Message}");

                return new List<RouteLegResultDto>();
            }
        }
        /// <summary>
        /// How early a driver is allowed to be told to arrive at a pickup.
        /// </summary>
        /// <remarks>
        /// Business rule: a driver must not reach the pickup more than fifteen minutes before
        /// the start of the pick-up window. Arriving earlier means a vehicle idling at a
        /// patient's door, and a patient who feels rushed out of a home or a clinic.
        /// Raphael.Desktop enforces the same limit when it routes a trip
        /// (<c>SchedulesViewModel</c>, the <c>pViolationLimit</c> block).
        /// </remarks>
        private static readonly TimeSpan EarlyArrivalLimit = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The same limit on a return leg, where it is shorter.
        /// </summary>
        /// <remarks>
        /// ⚠️ Raphael.Desktop has always used five minutes here and this app used fifteen for
        /// everything, so on a return trip the dispatcher and the driver were reading arrival
        /// hours ten minutes apart for the same stop — each one right by its own arithmetic. The
        /// dispatcher's rule is the one that stands: on a return the patient has finished at the
        /// clinic and is waiting to be collected, so holding the vehicle a quarter of an hour
        /// down the road buys nobody anything.
        /// </remarks>
        private static readonly TimeSpan ReturnEarlyArrivalLimit = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Caps an ETA that would put the driver at a pickup too early.
        /// </summary>
        /// <remarks>
        /// ⚠️ Pickups only. A dropoff has no such limit — the patient is already in the vehicle,
        /// and arriving early there is simply arriving early. Pull-out and Pull-in are excluded
        /// too: their <see cref="ScheduleDto.Pickup"/> is a sentinel (00:00 and 23:00), not a
        /// real window, and clamping against it would push every ETA of the day to 23:45.
        /// They have no <see cref="ScheduleDto.EventType"/>, which is what keeps them out.
        ///
        /// <para>
        /// What is capped is the arrival time, not <see cref="ScheduleDto.Travel"/>: the drive
        /// still takes what it takes. The difference is the driver waiting, which is the point.
        /// </para>
        /// </remarks>
        private static TimeSpan ApplyEarlyArrivalLimit(TimeSpan calculatedEta, ScheduleDto nextEvent)
        {
            if (nextEvent.EventType != ScheduleEventType.Pickup)
                return calculatedEta;

            if (nextEvent.Pickup is not { } windowStart)
                return calculatedEta;

            // Plain ordinal equality, like every other place in this ecosystem that asks the same
            // question — the two converters next door and SchedulesViewModel.ChainEtas. Being
            // cleverer here than the screen this has to agree with is how the ten minutes got
            // lost in the first place.
            var limit = nextEvent.TripType == "Return"
                ? ReturnEarlyArrivalLimit
                : EarlyArrivalLimit;

            var earliestAllowed = windowStart - limit;

            return calculatedEta < earliestAllowed
                ? earliestAllowed
                : calculatedEta;
        }

        public async Task<bool> UpdateETAAsync(ScheduleDto schedule)
        {
            if (schedule == null) return false;

            // We create an anonymous object with the structure expected by the backend
            var updateDto = new
            {
                ETA = schedule.ETA,
                Travel = schedule.Travel
            };

            var jsonContent = JsonSerializer.Serialize(updateDto, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
           
            var requestUri = $"api/Schedules/{schedule.Id}/update-eta";

            try
            {               
                var response = await _httpClient.PatchAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Error updating ETA: {response.StatusCode}. Body: {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in UpdateETAAsync: {ex.Message}");
                return false;
            }
        }

    }
}
