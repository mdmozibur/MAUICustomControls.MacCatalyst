using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// MAUI's ScrollViewHandler, with a scroll view that follows its content when the content gets
/// smaller.
///
/// A scroll view that is not stretched (UWP's VerticalAlignment="Top", or one in an Auto row) is as
/// tall as its content. MAUI's scroll view keeps a measure invalidation of its content to itself and
/// tells its ancestors only when the size it arranged the content at changes. Content that got
/// smaller than the scroll view is arranged to fill it, so that size does not change: the scroll
/// view kept its old height and the content was stretched over it (the SmartCAD left pane's star
/// rows spread out after switching the drawing tools from the detailed to the compact style).
/// </summary>
public class ContentSizedScrollViewHandler : ScrollViewHandler
{
    protected override UIScrollView CreatePlatformView() => new ContentSizedScrollView();

    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);
        if (PlatformView is ContentSizedScrollView scrollView)
        {
            scrollView.ScrollView = view as IScrollView;
        }
    }
}

/// <summary>
/// A MauiScrollView that has its ancestors measured again when the size its content asks for has
/// changed along a scrolling axis, whatever size the content was then arranged at.
/// </summary>
public class ContentSizedScrollView : MauiScrollView
{
    private WeakReference<IScrollView>? _scrollView;
    private Size? _lastContentSize;

    internal IScrollView? ScrollView
    {
        get => _scrollView is not null && _scrollView.TryGetTarget(out var scrollView) ? scrollView : null;
        set
        {
            _scrollView = value is null ? null : new WeakReference<IScrollView>(value);
            _lastContentSize = null;
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        if (ScrollView is not { PresentedContent: { } content } scrollView)
        {
            _lastContentSize = null;
            return;
        }

        // Only the axes the view scrolls along: across the other one the content is given the
        // view's own size, which says nothing about the size the view needs.
        var desired = content.DesiredSize;
        var contentSize = new Size(
            scrollView.Orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both ? desired.Width : 0,
            scrollView.Orientation is ScrollOrientation.Vertical or ScrollOrientation.Both ? desired.Height : 0);

        // The first layout follows the measure that sized the view, so there is nothing to report.
        if (_lastContentSize is { } last && last != contentSize)
        {
            LazyLayoutHandler.InvalidateAncestorsMeasures(this);
        }

        _lastContentSize = contentSize;
    }
}
