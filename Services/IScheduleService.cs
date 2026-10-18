
using Raphael.Driver.DTOs;

namespace Raphael.Driver.Services
{
    public interface IScheduleService
    {
        Task<List<ScheduleDto>> GetSchedulesByRunAsync(string runLogin, DateTime date);
        Task<List<ScheduleDto>> GetTodayScheduleAsync();
        Task<bool> UpdateScheduleAsync(ScheduleDto scheduleToUpdate);
        Task<bool> PerformScheduleUpdateAsync(ScheduleDto scheduleToUpdate);
        Task<bool> SaveSignatureAsync(int scheduleId, string signatureBase64);
        Task<byte[]?> GetSignatureAsync(int scheduleId);
        Task<bool> CancelTripByDriverAsync(int tripId, string reason);
        /// <summary>Tells the server the driver set off towards the pickup (RE-057).</summary>
        Task<StartTripOutcome> StartTripAsync(int tripId, TimeSpan? travel);

        Task<List<ScheduleDto>> GetPendingSchedulesByRunAsync(string runLogin, DateTime date);
        Task<List<ScheduleDto>> GetNextDaySchedulesByRunAsync(string runLogin);
        Task<List<ScheduleHistoryDto>> GetScheduleHistoryAsync(string runLogin, DateTime date);
        Task<int> GetScheduleHistoryCountAsync(string runLogin, DateTime date);
        Task<bool> UpdateContactPhoneNumberAsync(int tripId, string newPhoneNumber);

        Task UpdateNextSchedulesETAsAsync(List<ScheduleDto> allPendingEvents, int currentEventId, TimeSpan actualPerformTime);
        Task<bool> UpdateETAAsync(ScheduleDto schedule);
    }
}
