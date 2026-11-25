namespace Raphael.Driver.Services
{
    /// <summary>
    /// The identifier this device is reachable by for push notifications.
    /// </summary>
    /// <remarks>
    /// Android and iOS (RE-079) share the same Firebase project. Where Firebase is not
    /// configured this returns nothing and the in-app channel carries everything.
    /// </remarks>
    public interface IPushTokenProvider
    {
        bool IsSupported { get; }

        /// <summary>Asks the operating system for permission to show notifications.</summary>
        Task<bool> RequestPermissionAsync();

        /// <summary>The FCM registration token, or null when there is none to be had.</summary>
        Task<string?> GetTokenAsync();
    }
}
