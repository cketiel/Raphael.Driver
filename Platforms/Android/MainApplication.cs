using Android.App;
using Android.Runtime;

namespace Raphael.Driver
{
#if LOCAL_API
    // RE-059: plain http only in a build made for the local API (RaphaelApiEnvironment=Local).
    // A release build is Prod, which the release gate checks, and keeps Android's default: https only.
    [Application(UsesCleartextTraffic = true)]
#else
    [Application]
#endif
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
