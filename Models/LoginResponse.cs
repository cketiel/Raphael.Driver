using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raphael.Driver.Models
{
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Buys a new access token when the current one expires, so the driver is not sent
        /// back to the sign-in screen in the middle of a route. Single use: every renewal
        /// replaces it. Absent when talking to a server that predates refresh tokens, and the
        /// application behaves exactly as it used to in that case.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        public DateTime AccessTokenExpiresAtUtc { get; set; }

        public DateTime RefreshTokenExpiresAtUtc { get; set; }
    }
}
