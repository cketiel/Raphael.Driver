using Raphael.Driver.Helpers;

namespace Raphael.Driver.Services.Auth
{
    /// <summary>
    /// Tells the server which application this is and which build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server has read these since <c>v1.0.0</c> and no client ever sent them, so its
    /// telemetry could not tell a driver's phone from a dispatcher's desk, and its
    /// compatibility floor matched nobody.
    /// </para>
    /// <para>
    /// ⚠️ <c>X-Client-Id</c> also exists in this application and is <b>not</b> this one. It
    /// belongs to Zonitel and goes to the telephone company.
    /// </para>
    /// </remarks>
    public sealed class ClientVersionHandler : DelegatingHandler
    {
        /// <summary>
        /// ⚠️ Matched case-insensitively against two sections of the server's configuration:
        /// <c>ClientCompatibility:MinimumVersions</c>, which decides whether this build is
        /// reported as outdated, and <c>SessionPolicy:Apps</c>, which decides how long its
        /// sessions last — 30 days sliding for a driver, against 60 minutes for anything the
        /// server does not recognise. A typo here fails silently in both.
        /// </summary>
        public const string ApplicationName = "Driver";

        private const string StatusHeader = "X-Raphael-Client-Status";
        private const string MinimumHeader = "X-Raphael-Client-Minimum";

        /// <summary>True once the server has reported this build as below its floor.</summary>
        public static bool IsOutdated { get; private set; }

        /// <summary>The oldest build the server expects, when it has said so.</summary>
        public static string MinimumVersion { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-Client-App", ApplicationName);
            request.Headers.TryAddWithoutValidation("X-Client-Version", AppVersion.Current);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!IsOutdated &&
                response.Headers.TryGetValues(StatusHeader, out var status) &&
                status.Any(v => string.Equals(v, "outdated", StringComparison.OrdinalIgnoreCase)))
            {
                IsOutdated = true;

                MinimumVersion = response.Headers.TryGetValues(MinimumHeader, out var minimum)
                    ? minimum.FirstOrDefault()
                    : null;
            }

            // ⚠️ Recorded and nothing else. Never a gate, and on this application never even a
            // prompt: interrupting a driver mid-route over a version number is worse than
            // anything an old build gets wrong, and they cannot install a new one from the van
            // anyway. RELEASES.md:82-85.
            return response;
        }
    }
}
