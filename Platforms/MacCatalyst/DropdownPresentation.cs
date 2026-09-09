using CoreGraphics;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Positions the drop-down directly under its field and animates it out of the field's
/// bottom edge, mirroring the SwiftUI version.
/// </summary>
/// <remarks>
/// This replaces the popover presentation. A popover positions itself: with an arrow it
/// points at the source rect, and with the arrow suppressed it places itself wherever it
/// likes — neither of which is "flush under the field". A custom presentation controller
/// is the only way to state the frame outright.
/// </remarks>
internal sealed class DropdownPresentationController : UIPresentationController
{
    private readonly UIView _sourceView;
    private readonly double _gap;
    private readonly Action _onOutsideTap;

    private UIView? _touchCatcher;

    public DropdownPresentationController(
        UIViewController presentedViewController,
        UIViewController? presentingViewController,
        UIView sourceView,
        double gap,
        Action onOutsideTap)
        : base(presentedViewController, presentingViewController)
    {
        _sourceView = sourceView;
        _gap = gap;
        _onOutsideTap = onOutsideTap;
    }

    public override CGRect FrameOfPresentedViewInContainerView
    {
        get
        {
            if (ContainerView is not { } container)
                return CGRect.Empty;

            // The field's frame in the container's coordinate space is what anchors everything.
            var fieldRect = _sourceView.ConvertRectToView(_sourceView.Bounds, container);
            var preferred = PresentedViewController.PreferredContentSize;

            var width = preferred.Width > 0 ? (double)preferred.Width : (double)fieldRect.Width;

            // Stay inside the safe area, so the drop-down never slides under the title bar.
            var insets = container.SafeAreaInsets;
            var topLimit = (double)insets.Top;
            var bottomLimit = (double)container.Bounds.Height - (double)insets.Bottom;

            var spaceBelow = bottomLimit - ((double)fieldRect.GetMaxY() + _gap);
            var spaceAbove = (double)fieldRect.Y - _gap - topLimit;

            // Prefer below; flip up when below cannot hold the list but above can. When
            // neither fits, take the roomier side and let the list scroll in what is left.
            var desiredHeight = (double)preferred.Height;
            var openDownwards = desiredHeight <= spaceBelow || spaceBelow >= spaceAbove;

            var available = Math.Max(0, openDownwards ? spaceBelow : spaceAbove);
            var height = Math.Min(desiredHeight, available);

            var y = openDownwards
                ? (double)fieldRect.GetMaxY() + _gap
                : (double)fieldRect.Y - _gap - height;

            y = Math.Clamp(y, topLimit, Math.Max(topLimit, bottomLimit - height));

            var x = Math.Clamp((double)fieldRect.X, 0, Math.Max(0, (double)container.Bounds.Width - width));

            return new CGRect(x, y, width, height);
        }
    }

    public override void PresentationTransitionWillBegin()
    {
        base.PresentationTransitionWillBegin();

        if (ContainerView is not { } container)
            return;

        // Invisible, but it is what makes an outside click dismiss the drop-down.
        _touchCatcher = new UIView(container.Bounds)
        {
            BackgroundColor = UIColor.Clear,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
        };
        _touchCatcher.AddGestureRecognizer(new UITapGestureRecognizer(() => _onOutsideTap()));
        container.AddSubview(_touchCatcher);
    }

    public override void DismissalTransitionDidEnd(bool completed)
    {
        base.DismissalTransitionDidEnd(completed);

        if (completed)
        {
            _touchCatcher?.RemoveFromSuperview();
            _touchCatcher = null;
        }
    }
}

/// <summary>Supplies the presentation controller and the open/close animations.</summary>
internal sealed class DropdownTransitioningDelegate : UIViewControllerTransitioningDelegate
{
    private readonly UIView _sourceView;
    private readonly double _gap;
    private readonly Action _onOutsideTap;

    public DropdownTransitioningDelegate(UIView sourceView, double gap, Action onOutsideTap)
    {
        _sourceView = sourceView;
        _gap = gap;
        _onOutsideTap = onOutsideTap;
    }

    public override UIPresentationController GetPresentationControllerForPresentedViewController(
        UIViewController presentedViewController,
        UIViewController? presentingViewController,
        UIViewController sourceViewController) =>
        new DropdownPresentationController(
            presentedViewController, presentingViewController, _sourceView, _gap, _onOutsideTap);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForPresentedController(
        UIViewController presented, UIViewController presenting, UIViewController source) =>
        new DropdownAnimator(presenting: true);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForDismissedController(
        UIViewController dismissed) =>
        new DropdownAnimator(presenting: false);
}

/// <summary>Grows the list out of the field's top edge, and reverses on close.</summary>
internal sealed class DropdownAnimator : UIViewControllerAnimatedTransitioning
{
    private const float CollapsedScale = 0.94f;
    private const double PresentDuration = 0.11;
    private const double DismissDuration = 0.22;

    private readonly bool _presenting;

    public DropdownAnimator(bool presenting) => _presenting = presenting;

    public override double TransitionDuration(IUIViewControllerContextTransitioning? transitionContext) =>
        _presenting ? PresentDuration : DismissDuration;

    public override void AnimateTransition(IUIViewControllerContextTransitioning transitionContext)
    {
        if (_presenting)
            AnimateIn(transitionContext);
        else
            AnimateOut(transitionContext);
    }

    private static void AnimateIn(IUIViewControllerContextTransitioning context)
    {
        var toController = context.GetViewControllerForKey(UITransitionContext.ToViewControllerKey);
        var toView = context.GetViewFor(UITransitionContext.ToViewKey) ?? toController?.View;

        if (toController is null || toView is null)
        {
            context.CompleteTransition(true);
            return;
        }

        context.ContainerView.AddSubview(toView);
        toView.Frame = context.GetFinalFrameForViewController(toController);

        AnchorToTop(toView);
        toView.Alpha = 0;
        toView.Transform = CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);

        UIView.Animate(PresentDuration, 0, UIViewAnimationOptions.CurveEaseOut,
            () =>
            {
                toView.Alpha = 1;
                toView.Transform = CGAffineTransform.MakeIdentity();
            },
            () => context.CompleteTransition(!context.TransitionWasCancelled));
    }

    private static void AnimateOut(IUIViewControllerContextTransitioning context)
    {
        var fromView = context.GetViewFor(UITransitionContext.FromViewKey)
                       ?? context.GetViewControllerForKey(UITransitionContext.FromViewControllerKey)?.View;

        if (fromView is null)
        {
            context.CompleteTransition(true);
            return;
        }

        UIView.Animate(DismissDuration, 0, UIViewAnimationOptions.CurveEaseIn,
            () =>
            {
                fromView.Alpha = 0;
                fromView.Transform = CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);
            },
            () =>
            {
                fromView.RemoveFromSuperview();
                context.CompleteTransition(!context.TransitionWasCancelled);
            });
    }

    /// <summary>
    /// Moves the layer's anchor point to the top edge so the scale grows downwards.
    /// Re-assigning the frame afterwards is what keeps the view in place.
    /// </summary>
    private static void AnchorToTop(UIView view)
    {
        var frame = view.Frame;
        view.Layer.AnchorPoint = new CGPoint(0.5, 0);
        view.Frame = frame;
    }
}
