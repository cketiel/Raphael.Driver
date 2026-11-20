using Foundation;
using UIKit;
using UserNotifications;
using Raphael.Driver.Services;
using Debug = System.Diagnostics.Debug;

namespace Raphael.Driver
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            var result = base.FinishedLaunching(application, launchOptions);

            // Without GoogleService-Info.plist there is no Firebase project to talk to: the app
            // runs without push and the in-app channel carries everything, as before.
            if (NSBundle.MainBundle.PathForResource("GoogleService-Info", "plist") is not null)
            {
                try
                {
                    Firebase.Core.App.Configure();
                    Firebase.CloudMessaging.Messaging.SharedInstance.Delegate = new FcmDelegate();
                    UNUserNotificationCenter.Current.Delegate = new ForegroundNotificationDelegate();
                    application.RegisterForRemoteNotifications();
                    PushTokenProvider.FirebaseConfigured = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppDelegate: Firebase could not start. {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>Firebase rotates tokens on its own schedule; same handling as Android's OnNewToken.</summary>
        private sealed class FcmDelegate : NSObject, Firebase.CloudMessaging.IMessagingDelegate
        {
            [Export("messaging:didReceiveRegistrationToken:")]
            public void DidReceiveRegistrationToken(Firebase.CloudMessaging.Messaging messaging, string fcmToken)
            {
                if (string.IsNullOrEmpty(fcmToken))
                    return;

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var session = ServiceHelper.GetService<NotificationSessionService>();
                        if (session is not null)
                            await session.SendTokenAsync(fcmToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AppDelegate: could not send the token. {ex.Message}");
                    }
                });
            }
        }

        /// <summary>iOS hides pushes while the app is open unless told otherwise; Android shows them.</summary>
        private sealed class ForegroundNotificationDelegate : UNUserNotificationCenterDelegate
        {
            public override void WillPresentNotification(UNUserNotificationCenter center,
                UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
            {
                completionHandler(UNNotificationPresentationOptions.Banner
                                  | UNNotificationPresentationOptions.List
                                  | UNNotificationPresentationOptions.Sound);
            }

            // The tap on the push, with the app running or closed: the same as Android's
            // MainActivity.HandleNotificationIntent. The delegate is set inside FinishedLaunching,
            // so iOS also delivers here the tap that launched the app from closed.
            public override void DidReceiveNotificationResponse(UNUserNotificationCenter center,
                UNNotificationResponse response, Action completionHandler)
            {
                try
                {
                    if (response.ActionIdentifier == UNNotificationAction.DefaultActionIdentifier)
                        NotificationRouter.RequestOpen();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppDelegate: could not route the notification tap. {ex.Message}");
                }
                finally
                {
                    completionHandler();
                }
            }
        }
    }
}
