using System.Diagnostics;

#if ANDROID
using Firebase.Messaging;
#endif
#if IOS
using UserNotifications;
#endif

namespace Raphael.Driver.Services
{
    public class PushTokenProvider : IPushTokenProvider
    {
        /// <summary>
        /// Where the last token seen is kept, so the app can tell a new one from a repeat and
        /// avoid re-registering the same value on every sign in.
        /// </summary>
        public const string TokenPreferenceKey = "FcmToken";

#if IOS
        /// <summary>Set by AppDelegate once Firebase started (GoogleService-Info.plist present).</summary>
        public static bool FirebaseConfigured { get; set; }
#endif

#if ANDROID
        public bool IsSupported => true;
#elif IOS
        public bool IsSupported => FirebaseConfigured;
#else
        public bool IsSupported => false;
#endif

        public async Task<bool> RequestPermissionAsync()
        {
#if ANDROID
            try
            {
                // Android 13+ asks the user. Below that the permission is granted at install
                // time and the request returns immediately.
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();

                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.PostNotifications>();

                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PushTokenProvider: permission request failed. {ex.Message}");
                return false;
            }
#elif IOS
            try
            {
                var (granted, _) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
                    UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
                return granted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PushTokenProvider: permission request failed. {ex.Message}");
                return false;
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public async Task<string?> GetTokenAsync()
        {
#if ANDROID
            try
            {
                var completion = new TaskCompletionSource<string?>();

                FirebaseMessaging.Instance
                    .GetToken()
                    .AddOnCompleteListener(new TokenListener(completion));

                // Firebase can sit on this call when the device has no network. A driver
                // starting a shift underground must not be left staring at a login screen.
                var finished = await Task.WhenAny(
                    completion.Task,
                    Task.Delay(TimeSpan.FromSeconds(15)));

                return finished == completion.Task
                    ? await completion.Task
                    : null;
            }
            catch (Exception ex)
            {
                // No Firebase configuration on the device means no push. The inbox and the live
                // channel keep working, so this is degraded, not broken.
                Debug.WriteLine($"PushTokenProvider: could not get the FCM token. {ex.Message}");
                return null;
            }
#elif IOS
            if (!FirebaseConfigured)
                return null;

            try
            {
                var completion = new TaskCompletionSource<string?>();

                Firebase.CloudMessaging.Messaging.SharedInstance.FetchToken((token, error) =>
                    completion.TrySetResult(error is null ? token : null));

                // Same 15 s limit as Android: no network must not block the sign in.
                var finished = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(15)));
                return finished == completion.Task ? await completion.Task : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PushTokenProvider: could not get the FCM token. {ex.Message}");
                return null;
            }
#else
            await Task.CompletedTask;
            return null;
#endif
        }

#if ANDROID
        private sealed class TokenListener :
            Java.Lang.Object,
            Android.Gms.Tasks.IOnCompleteListener
        {
            private readonly TaskCompletionSource<string?> _completion;

            public TokenListener(TaskCompletionSource<string?> completion)
            {
                _completion = completion;
            }

            public void OnComplete(Android.Gms.Tasks.Task task)
            {
                try
                {
                    _completion.TrySetResult(
                        task.IsSuccessful ? task.Result?.ToString() : null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PushTokenProvider: {ex.Message}");
                    _completion.TrySetResult(null);
                }
            }
        }
#endif
    }
}
