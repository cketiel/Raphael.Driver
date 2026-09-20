using Microsoft.Maui.ApplicationModel;

namespace Raphael.Driver.Helpers
{
    /// <summary>
    /// Provides application version information.
    /// </summary>
    public static class AppVersion
    {
        /// <summary>
        /// Gets the current application version configured for the MAUI application.
        /// </summary>
        public static string Current => AppInfo.Current.VersionString;

        /// <summary>
        /// Gets the application version formatted for display.
        /// </summary>
        /// <remarks>
        /// ⚠️ The build number in brackets is the Android versionCode, and it is here because
        /// the version alone cannot answer "which build is on this phone?". Two builds of
        /// 1.5.0 can exist — a candidate under test and the one that ships — and they look
        /// identical on screen while behaving differently. The versionCode never repeats, so
        /// it is the only thing on screen that identifies a build exactly. It is also the
        /// first thing to ask for on a support call.
        /// </remarks>
        public static string Display => $"Version {Current} ({AppInfo.Current.BuildString})";
    }
}