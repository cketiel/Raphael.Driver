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
        void Logout();
    }
}
