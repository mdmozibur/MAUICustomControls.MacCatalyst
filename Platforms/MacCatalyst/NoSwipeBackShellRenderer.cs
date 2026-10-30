using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The Shell without UIKit's swipe-back. Each Shell section is a UINavigationController, whose
/// interactive pop gestures on the Mac follow a two-finger horizontal trackpad swipe: swiping right
/// on the drawing page popped it and showed the page beneath. UWP's Frame goes back only when the
/// app calls GoBack, so the gesture is off; programmatic pops are unaffected.
/// </summary>
public class NoSwipeBackShellRenderer : ShellRenderer
{
    protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection) =>
        new NoSwipeBackShellSectionRenderer(this);

    private sealed class NoSwipeBackShellSectionRenderer : ShellSectionRenderer
    {
        public NoSwipeBackShellSectionRenderer(IShellContext context)
            : base(context)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            if (InteractivePopGestureRecognizer is { } popGesture)
            {
                popGesture.Enabled = false;
            }

            // Since iOS 26 / Mac Catalyst 26 a second recognizer pops from anywhere in the content,
            // not just the leading edge; on the Mac it is the one a trackpad swipe drives.
            if ((OperatingSystem.IsMacCatalystVersionAtLeast(26) || OperatingSystem.IsIOSVersionAtLeast(26)) &&
                InteractiveContentPopGestureRecognizer is { } contentPopGesture)
            {
                contentPopGesture.Enabled = false;
            }
        }
    }
}
