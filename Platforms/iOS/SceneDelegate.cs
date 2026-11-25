using Foundation;

namespace Raphael.Driver
{
    /// <summary>
    /// UIKit scene delegate. Apps built with the Xcode 27 SDK must adopt the scene life cycle
    /// or iOS closes them right after the launch screen. The name must match
    /// UISceneDelegateClassName in Info.plist.
    /// </summary>
    [Register("SceneDelegate")]
    public class SceneDelegate : MauiUISceneDelegate
    {
    }
}
