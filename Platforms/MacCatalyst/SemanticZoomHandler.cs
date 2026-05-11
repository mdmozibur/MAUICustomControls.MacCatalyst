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
        [nameof(IContentView.Content)] = MapContent
    };

    public SemanticZoomHandler() : base(PropertyMapper)
    {
    }

    protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        base.ConnectHandler(platformView);

        if (VirtualView is SemanticZoom semanticZoom)
        {
            semanticZoom.PlatformAnimateTransition = AnimateTransition;
        }
    }

    protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        if (VirtualView is SemanticZoom semanticZoom)
        {
            semanticZoom.PlatformAnimateTransition = null;
        }

        base.DisconnectHandler(platformView);
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
