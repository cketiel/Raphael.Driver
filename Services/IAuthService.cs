using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raphael.Driver.Models;

namespace Raphael.Driver.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);

        /// <summary>
        /// Ends the session: stops notifications and GPS, revokes on the server, and returns
        /// to the sign-in screen.
        /// </summary>
        /// <remarks>
        /// ⚠️ It returns a <see cref="Task"/> and it must be awaited. It used to be
        /// <c>void</c>, which made it read like something cheap and led to it blocking the UI
        /// thread on secure storage — and freezing the phone solid the moment a driver signed
        /// out. A signature that cannot be misread is half the fix.
        /// </remarks>
        Task LogoutAsync();
    }
}
