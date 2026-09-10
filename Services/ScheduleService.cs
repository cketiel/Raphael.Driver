

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Raphael.Driver.DTOs;
using Raphael.Driver.Models;

namespace Raphael.Driver.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;
        public ScheduleService(HttpClient httpClient)
        {
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

        public async Task<List<ScheduleDto>> GetSchedulesByRunAsync(string runLogin, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(runLogin))
                return new List<ScheduleDto>(); 

            var dateString = date.ToString("yyyy-MM-dd");
            //dateString = "2025-09-15"; // esto es para probar
            var encodedRunLogin = HttpUtility.UrlEncode(runLogin);

            var requestUri = $"api/Schedules/by-run-login?runLogin={encodedRunLogin}&date={dateString}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var schedules = JsonSerializer.Deserialize<List<ScheduleDto>>(content, _serializerOptions);
                    return schedules ?? new List<ScheduleDto>();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching schedule: {response.StatusCode}");
                    return new List<ScheduleDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetSchedulesByRunAsync: {ex.Message}");              
                throw;
            }
        }

        public async Task<List<ScheduleDto>> GetPendingSchedulesByRunAsync(string runLogin, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(runLogin))
                return new List<ScheduleDto>();

            var dateString = date.ToString("yyyy-MM-dd");
            //dateString = "2025-04-23"; // esto es para probar
            var encodedRunLogin = HttpUtility.UrlEncode(runLogin);
          
            var requestUri = $"api/Schedules/driver/pending?runLogin={encodedRunLogin}&date={dateString}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);
                 
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var schedules = JsonSerializer.Deserialize<List<ScheduleDto>>(content, _serializerOptions);
                    return schedules ?? new List<ScheduleDto>();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching schedule: {response.StatusCode}");
                    return new List<ScheduleDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in GetSchedulesByRunAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateScheduleAsync(ScheduleDto scheduleToUpdate)
        {
            if (scheduleToUpdate == null)
                return false;

            // Prepares JSON content to send in the request body
            var jsonContent = JsonSerializer.Serialize(scheduleToUpdate, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
         
            var requestUri = $"api/Schedules/{scheduleToUpdate.Id}";

            try
            {
                // The HTTP PUT verb is used, which is the standard for full updates to a resource
                var response = await _httpClient.PutAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {                   
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error updating schedule: {response.StatusCode}. Body: {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in UpdateScheduleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PerformScheduleUpdateAsync(ScheduleDto scheduleToUpdate)
        {
            if (scheduleToUpdate == null)
                return false;
          
            var jsonContent = JsonSerializer.Serialize(scheduleToUpdate, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
           
            var requestUri = $"api/Schedules/{scheduleToUpdate.Id}/perform";

            try
            {               
                var response = await _httpClient.PutAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Error in PerformScheduleUpdateAsync: {response.StatusCode}. Body: {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in PerformScheduleUpdateAsync: {ex.Message}");
                return false;
            }
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
            var requestUri = $"api/Schedules/{scheduleId}/signature";
           
            var uploadDto = new SignatureUploadDto { SignatureBase64 = signatureBase64 };

            var jsonContent = JsonSerializer.Serialize(uploadDto, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error saving signature: {response.StatusCode}. Body: {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in SaveSignatureAsync: {ex.Message}");
                return false;
            }
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

        public async Task<bool> CancelTripByDriverAsync(int tripId, string reason)
        {
            var requestUri = $"api/Trips/{tripId}/cancel-by-driver";
            var uploadDto = new DriverCancelTripDto { Reason = reason };

            var jsonContent = JsonSerializer.Serialize(uploadDto, _serializerOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(requestUri, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in CancelTripByDriverAsync: {ex.Message}");
                return false;
            }
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
            }
            catch (Exception ex)
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
            var requestUri = $"api/Schedules/trip/{tripId}/contact-phone";
            
            var updatePayload = new { PhoneNumber = newPhoneNumber };
            var jsonContent = JsonSerializer.Serialize(updatePayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PutAsync(requestUri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Error updating phone number: {response.StatusCode}. Body: {errorBody}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in UpdateContactPhoneNumberAsync: {ex.Message}");
                return false;
            }
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
