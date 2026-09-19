using System.Reflection;

namespace Raphael.Driver.Configuration
{
    /// <summary>
    /// Which server this phone talks to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Doctrine: <c>_meta/CLIENT_CONFIG_POLICY.md</c>. Before this, the address was a literal in
    /// four live places, and one of them —<c>AuthService</c>— overwrote the address dependency
    /// injection had just supplied. Changing only the obvious one moved every call except
    /// signing in, which is the worst possible split: the office would work in one database
    /// while the drivers authenticated against another.
    /// </para>
    /// <para>
    /// <b>Resolution order: on-device override, then the value compiled in.</b> There is no
    /// third source and no build-configuration switch. Choosing the server by
    /// <c>Debug</c>/<c>Release</c> would mean nobody could ever test a Release build against
    /// DEV — which is exactly what you want to do the day before shipping one.
    /// </para>
    /// <para>
    /// ⚠️ The compiled-in value is the custom domain, never the Azure host. With the domain,
    /// moving the backend for all 31 phones is a CNAME change with a one-hour TTL; with the
    /// Azure host it is 31 phones again.
    /// </para>
    /// </remarks>
    public static class ApiEnvironment
    {
        public const string Production = "Prod";
        public const string Development = "Dev";

        public const string ProductionUrl = "https://api.raphaeldh.com/";
        public const string DevelopmentUrl = "https://app-raphael-dev-scus.azurewebsites.net/";

        /// <summary>
        /// ⚠️ Deliberately NOT "ApiBaseUrl". That key was written by <c>App.xaml.cs</c> on every
        /// single start, so anything stored under it died at the next launch and it could never
        /// have worked as an override. Keeping the name would have inherited the confusion.
        /// </summary>
        private const string OverrideNameKey = "Raphael.Api.Environment";

        private const string OverrideUrlKey = "Raphael.Api.BaseUrl";

        /// <summary>The environment in use: <c>Prod</c>, <c>Dev</c>, or <c>Custom</c>.</summary>
        public static string Name { get; private set; } = Production;

        /// <summary>Base address of the API, always with one trailing slash.</summary>
        public static string BaseUrl { get; private set; } = ProductionUrl;

        public static bool IsProduction => Name == Production;

        /// <summary>
        /// True when somebody has pointed this phone somewhere by hand. Support needs to be
        /// able to ask "has this one been moved?" and get an answer.
        /// </summary>
        public static bool IsOverridden { get; private set; }

        /// <summary>
        /// Reads the override, if any, and otherwise what the build was compiled with. Called
        /// once, before anything can make a request.
        /// </summary>
        public static void Initialise()
        {
            var storedName = Preferences.Get(OverrideNameKey, string.Empty);
            var storedUrl = Preferences.Get(OverrideUrlKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(storedName) && !string.IsNullOrWhiteSpace(storedUrl))
            {
                Name = storedName;
                BaseUrl = Normalise(storedUrl);
                IsOverridden = true;
                return;
            }

            // What the csproj declared, carried into the binary so that the release gate's
            // check of <RaphaelApiEnvironment> corresponds to something the app actually reads.
            var compiled = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "RaphaelApiEnvironment")?.Value;

            Name = string.IsNullOrWhiteSpace(compiled) ? Production : compiled;
            BaseUrl = Normalise(UrlFor(Name));
            IsOverridden = false;
        }

        /// <summary>
        /// Points this phone at a different server, from the dialog on the sign-in screen.
        /// </summary>
        public static void SetOverride(string name, string url)
        {
            Preferences.Set(OverrideNameKey, name);
            Preferences.Set(OverrideUrlKey, Normalise(url));

            Name = name;
            BaseUrl = Normalise(url);
            IsOverridden = true;
        }

        /// <summary>Back to whatever this build was compiled for.</summary>
        public static void ClearOverride()
        {
            Preferences.Remove(OverrideNameKey);
            Preferences.Remove(OverrideUrlKey);
            Initialise();
        }

        /// <summary>
        /// Runs something that wipes all preferences, and puts this configuration back.
        /// </summary>
        /// <remarks>
        /// ⚠️ <c>AuthService.Logout()</c> calls <c>Preferences.Clear()</c>. Without this, signing
        /// out would erase the server somebody from support had just walked a driver through
        /// setting, and the phone would silently go back to the compiled default — which is the
        /// one thing a support call is trying to get away from. Written as save/clear/restore
        /// rather than a list of keys to keep, so that a preference added later still gets
        /// cleared exactly as it does today.
        /// </remarks>
        public static void PreserveAcross(Action clearEverything)
        {
            var name = Preferences.Get(OverrideNameKey, string.Empty);
            var url = Preferences.Get(OverrideUrlKey, string.Empty);

            clearEverything();

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
            {
                Preferences.Set(OverrideNameKey, name);
                Preferences.Set(OverrideUrlKey, url);
            }
        }

        /// <summary>The address that goes with a well-known environment name.</summary>
        public static string UrlFor(string name) => name switch
        {
            Development => DevelopmentUrl,
            Production => ProductionUrl,
            _ => ProductionUrl
        };

        private static string Normalise(string url) => url.Trim().TrimEnd('/') + "/";
    }
}
