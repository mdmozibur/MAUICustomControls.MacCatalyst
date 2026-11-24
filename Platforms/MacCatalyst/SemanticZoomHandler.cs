using CoreGraphics;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// MacCatalyst handler for <see cref="SemanticZoom"/> that provides smooth
/// native UIKit animations when transitioning between zoomed-in and zoomed-out views.
/// 
/// The transition mimics UWP SemanticZoom:
///   - Zooming out: current view scales down + fades out, overview scales up from small + fades in
///   - Zooming in:  overview scales down + fades out, detail view scales up from small + fades in
/// Uses spring-damped timing for a natural feel.
/// </summary>
public sealed class SemanticZoomHandler : ContentViewHandler
{
    private const double ScaleDown = 0.88;
    private const double ScaleUp = 1.08;
    private const double AnimationDuration = 0.32;
    private const double SpringDamping = 0.85;
    private const double SpringVelocity = 0.4;

    public static PropertyMapper<SemanticZoom, SemanticZoomHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(IContentView.Content)] = MapContent,
        [nameof(SemanticZoom.IsZoomedInViewHeaderSticky)] = MapIsZoomedInViewHeaderSticky
    };

    // UWP zooms out when a group header of the zoomed-in list is clicked. The click is a native
    // single-tap recognizer on that list which only takes clicks on a header and never delays or
    // cancels the list's own clicks, so selecting items works as before.
    private UICollectionView? _headerClickList;
    private UITapGestureRecognizer? _headerClick;
    private CollectionView? _waitingList;

    public SemanticZoomHandler() : base(PropertyMapper)
    {
    }

    protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is SemanticZoom semanticZoom)
        {
            semanticZoom.PlatformAnimateTransition = AnimateTransition;
            UpdateZoomedInViewHeaderStickiness(semanticZoom);
            AttachHeaderClick(semanticZoom);
            semanticZoom.Dispatcher.Dispatch(() => AttachHeaderClick(semanticZoom));
        }
    }

    protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        if (VirtualView is SemanticZoom semanticZoom)
        {
            semanticZoom.PlatformAnimateTransition = null;
        }

        DetachHeaderClick();
        base.DisconnectHandler(platformView);
    }

    public static void MapIsZoomedInViewHeaderSticky(SemanticZoomHandler handler, SemanticZoom view)
    {
        handler.UpdateZoomedInViewHeaderStickiness(view);
        view.Dispatcher.Dispatch(() => handler.UpdateZoomedInViewHeaderStickiness(view));
    }

    private void UpdateZoomedInViewHeaderStickiness(SemanticZoom semanticZoom)
    {
        var collectionView = FindCollectionView(semanticZoom.ZoomedInView);
        if (collectionView == null || MauiContext == null)
            return;

        if (GetPlatformView(collectionView, MauiContext) is not UICollectionView platformCollectionView ||
            platformCollectionView.CollectionViewLayout is not UICollectionViewFlowLayout layout)
        {
            return;
        }

        layout.SectionHeadersPinToVisibleBounds = semanticZoom.IsZoomedInViewHeaderSticky;
        layout.InvalidateLayout();
    }

    private void AttachHeaderClick(SemanticZoom semanticZoom)
    {
        var list = FindCollectionView(semanticZoom.ZoomedInView);
        if (list is null)
            return;

        if (FindNativeList(list.Handler?.PlatformView as UIView) is not { } nativeList)
        {
            // The list gets its native view later: wire the click once it has one.
            if (!ReferenceEquals(_waitingList, list))
            {
                StopWaiting();
                _waitingList = list;
                list.HandlerChanged += OnZoomedInListHandlerChanged;
            }
            return;
        }

        StopWaiting();
        if (ReferenceEquals(nativeList, _headerClickList))
            return;

        DetachHeaderClick();
        var click = new UITapGestureRecognizer(recognizer =>
        {
            if (recognizer.State == UIGestureRecognizerState.Recognized && VirtualView is SemanticZoom zoom)
                zoom.IsZoomedInViewActive = false;
        })
        {
            CancelsTouchesInView = false,
            DelaysTouchesBegan = false,
            DelaysTouchesEnded = false,
        };
        click.ShouldReceiveTouch = (_, touch) => IsOnGroupHeader(nativeList, touch.LocationInView(nativeList));
        nativeList.AddGestureRecognizer(click);
        _headerClickList = nativeList;
        _headerClick = click;
    }

    private void OnZoomedInListHandlerChanged(object? sender, EventArgs e)
    {
        if (VirtualView is SemanticZoom semanticZoom)
            semanticZoom.Dispatcher.Dispatch(() => AttachHeaderClick(semanticZoom));
    }

    private void StopWaiting()
    {
        if (_waitingList is { } list)
            list.HandlerChanged -= OnZoomedInListHandlerChanged;
        _waitingList = null;
    }

    private void DetachHeaderClick()
    {
        StopWaiting();
        if (_headerClickList is { } list && _headerClick is { } click)
            list.RemoveGestureRecognizer(click);
        _headerClickList = null;
        _headerClick = null;
    }

    private static bool IsOnGroupHeader(UICollectionView list, CGPoint point)
    {
        foreach (var header in list.GetVisibleSupplementaryViews(UICollectionElementKindSectionKey.Header))
        {
            if (header.Frame.Contains(point))
                return true;
        }

        return false;
    }

    // MAUI's CollectionView handler hosts the UICollectionView inside a container view.
    private static UICollectionView? FindNativeList(UIView? view)
    {
        if (view is null)
            return null;

        if (view is UICollectionView list)
            return list;

        foreach (var child in view.Subviews)
        {
            if (FindNativeList(child) is { } found)
                return found;
        }

        return null;
    }

    private static CollectionView? FindCollectionView(Element? root)
    {
        if (root == null)
            return null;

        if (root is CollectionView collectionView)
            return collectionView;

        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren())
        {
            if (child is Element element && FindCollectionView(element) is { } found)
                return found;
        }

        return null;
    }

    private void AnimateTransition(View incoming, View outgoing, bool zoomingOut, Action onComplete)
    {
        var mauiContext = MauiContext;
        if (mauiContext == null)
        {
            onComplete();
            return;
        }

        var incomingPlatform = GetPlatformView(incoming, mauiContext);
        var outgoingPlatform = GetPlatformView(outgoing, mauiContext);

        if (incomingPlatform == null || outgoingPlatform == null)
        {
            onComplete();
            return;
        }

        // Prepare incoming view: start scaled and fully transparent
        var initialScale = zoomingOut ? ScaleUp : ScaleDown;
        incomingPlatform.Transform = CGAffineTransform.MakeScale((nfloat)initialScale, (nfloat)initialScale);
        incomingPlatform.Alpha = 0;

        // Ensure both views are in the hierarchy and visible for the animation
        outgoingPlatform.Hidden = false;
        incomingPlatform.Hidden = false;

        // Animate using spring timing for a natural, polished feel
        UIView.AnimateNotify(
            duration: AnimationDuration,
            delay: 0,
            springWithDampingRatio: (nfloat)SpringDamping,
            initialSpringVelocity: (nfloat)SpringVelocity,
            options: UIViewAnimationOptions.CurveEaseInOut | UIViewAnimationOptions.AllowUserInteraction,
            animations: () =>
            {
                // Incoming: scale to 1.0 and fade in
                incomingPlatform.Transform = CGAffineTransform.MakeIdentity();
                incomingPlatform.Alpha = 1;

                // Outgoing: scale down/up and fade out
                var outScale = zoomingOut ? ScaleDown : ScaleUp;
                outgoingPlatform.Transform = CGAffineTransform.MakeScale((nfloat)outScale, (nfloat)outScale);
                outgoingPlatform.Alpha = 0;
            },
            completion: finished =>
            {
                // Reset outgoing view to neutral state
                outgoingPlatform.Transform = CGAffineTransform.MakeIdentity();
                outgoingPlatform.Alpha = 1;
                outgoingPlatform.Hidden = true;

                // Ensure incoming is fully settled
                incomingPlatform.Transform = CGAffineTransform.MakeIdentity();
                incomingPlatform.Alpha = 1;

                // Notify the control that the transition is complete
                onComplete();
            });
    }

    private static UIView? GetPlatformView(View mauiView, IMauiContext mauiContext)
    {
        if (mauiView.Handler?.PlatformView is UIView platformView)
        {
            return platformView;
        }

        // The view might not have a handler yet if it was invisible.
        // Force it to get one by converting to platform.
        try
        {
            return mauiView.ToPlatform(mauiContext);
        }
        catch
        {
            return null;
        }
    }
}
