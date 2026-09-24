using Microsoft.Maui.Handlers;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Turns off the soft edge (macOS 26) that UIKit draws over scrolled content under a title bar or
/// toolbar. A UWP app has no such effect; with the content extended into the title bar, every scroll
/// view in the band showed its content blurred there. Applies to ScrollView and to the collection
/// view inside a CollectionView/ListView.
/// </summary>
public static class ScrollEdgeEffects
{
    public static void Configure()
    {
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(26) && !OperatingSystem.IsIOSVersionAtLeast(26))
        {
            return;
        }

        ScrollViewHandler.Mapper.AppendToMapping("NoScrollEdgeEffects", (handler, _) => Hide(handler.PlatformView));
        ViewHandler.ViewMapper.AppendToMapping("NoScrollEdgeEffects", (handler, view) =>
        {
            if (view is not Microsoft.Maui.Controls.ItemsView || handler.PlatformView is not UIView platformView)
            {
                return;
            }

            // The collection view is created with the handler's view but may be added to it later.
            HideIn(platformView);
            platformView.BeginInvokeOnMainThread(() => HideIn(platformView));
        });
    }

    private static void HideIn(UIView view)
    {
        if (view is UIScrollView scrollView)
        {
            Hide(scrollView);
            return;
        }

        foreach (var subview in view.Subviews)
        {
            if (subview is UIScrollView nested)
            {
                Hide(nested);
            }
        }
    }

    /// <summary>Hides all four edge effects of <paramref name="scrollView"/>.</summary>
    public static void Hide(UIScrollView scrollView)
    {
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(26) && !OperatingSystem.IsIOSVersionAtLeast(26))
        {
            return;
        }

        scrollView.TopEdgeEffect.Hidden = true;
        scrollView.BottomEdgeEffect.Hidden = true;
        scrollView.LeftEdgeEffect.Hidden = true;
        scrollView.RightEdgeEffect.Hidden = true;
    }
}
