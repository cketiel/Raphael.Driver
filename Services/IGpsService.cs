using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raphael.Driver.Services
{
    public interface IGpsService
    {
        bool IsTracking { get; }
        void StartTracking(int vehicleRouteId);
        void StopTracking();
        Task<Location?> GetCurrentLocationAsync();

        /// <summary>
        /// Reports the current position right away instead of at the next tick. Only while
        /// tracking: outside a route there is no vehicle on anybody's map to move.
        /// </summary>
        Task SendNowAsync();

        // Event to notify state changes to the UI
        event Action<bool> IsTrackingChanged;
    }
}
