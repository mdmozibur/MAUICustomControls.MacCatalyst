using Foundation;

namespace MAUICustomControls.MacCatalyst;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

// The Mac Catalyst 27 SDK requires the UIScene life cycle (MT7199; the app crashes at launch on
// macOS 27 without it). Info.plist's UIApplicationSceneManifest names this delegate for MAUI's
// default scene configuration.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
