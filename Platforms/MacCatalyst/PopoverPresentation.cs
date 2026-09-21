using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui;
using MAUICustomControls.MacCatalyst.Controls;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>Visual settings for one popover, read from the <see cref="PopoverButton"/> when it opens.</summary>
/// <param name="Stroke">Border colour; null draws the system separator hairline.</param>
/// <param name="StrokeThickness">Border width in points; 0 is one device pixel.</param>
internal readonly record struct PopoverChromeOptions(double CornerRadius, bool ShowArrow, Thickness Padding, UIColor? Stroke = null, double StrokeThickness = 0);


/// <summary>Which side of the source view the popover sits on.</summary>
internal enum PopoverSide
{
    Above,
    Below,
    Left,
    Right
}

/// <summary>
/// Hosts the popover's MAUI content. The handler keeps one instance alive between openings,
/// so reopening a popover does not rebuild its native views.
/// </summary>
internal sealed class PopoverHostViewController : UIViewController
{
    private readonly UIView _content;
    private readonly Action _onEscape;

    public PopoverHostViewController(UIView content, Action onEscape)
    {
        _content = content;
        _onEscape = onEscape;
    }

    public override void LoadView() => View = _content;

    public override bool CanBecomeFirstResponder => true;

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        BecomeFirstResponder();
    }

    public override UIKeyCommand[] KeyCommands =>
    [
        UIKeyCommand.Create(UIKeyCommand.Escape, (UIKeyModifierFlags)0, new Selector("dismissPopover:"))
    ];

    [Export("dismissPopover:")]
    public void DismissPopover(UIKeyCommand command) => _onEscape();
}

/// <summary>
/// The popover's bubble: background, hairline border, shadow and an optional arrow pointing
/// at the source. The content is clipped to the rounded body.
/// </summary>
internal sealed class PopoverChromeView : UIView
{
    public const double ArrowLength = 7;
    public const double ArrowHalfWidth = 8;

    private readonly UIView _contentView;
    private readonly UIView _clipView;
    private readonly CAShapeLayer _backgroundLayer = new();
    private readonly PopoverChromeOptions _options;

    public PopoverChromeView(UIView contentView, PopoverChromeOptions options)
    {
        _contentView = contentView;
        _options = options;

        BackgroundColor = UIColor.Clear;
        Layer.ShadowColor = UIColor.Black.CGColor;
        Layer.ShadowOpacity = 0.18f;
        Layer.ShadowRadius = 8;
        Layer.ShadowOffset = new CGSize(0, 4);
        Layer.AddSublayer(_backgroundLayer);

        _clipView = new UIView { BackgroundColor = UIColor.Clear, ClipsToBounds = true };
        _clipView.Layer.CornerRadius = (nfloat)Math.Max(0, options.CornerRadius);
        _clipView.AddSubview(contentView);
        AddSubview(_clipView);
    }

    public PopoverSide Side { get; set; }

    /// <summary>Where the arrow tip points along its edge, in this view's coordinates.</summary>
    public double ArrowPosition { get; set; }

    // The bubble's layer colours are resolved in LayoutSubviews; lay out again when the appearance
    // (light/dark) changes so they are re-resolved.
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        SetNeedsLayout();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var body = GetBodyRect();
        var padding = _options.Padding;

        _clipView.Frame = body;
        _contentView.Frame = new CGRect(
            padding.Left,
            padding.Top,
            Math.Max(0, (double)body.Width - padding.HorizontalThickness),
            Math.Max(0, (double)body.Height - padding.VerticalThickness));

        var outline = CreateOutline(body);

        // A standalone layer animates every property change implicitly; the bubble must track
        // its frame exactly, including during the open animation.
        CATransaction.Begin();
        CATransaction.DisableActions = true;
        _backgroundLayer.Frame = Bounds;
        _backgroundLayer.Path = outline;
        _backgroundLayer.FillColor = UIColor.SecondarySystemBackground.GetResolvedColor(TraitCollection).CGColor;
        _backgroundLayer.StrokeColor = (_options.Stroke ?? UIColor.Separator).GetResolvedColor(TraitCollection).CGColor;
        _backgroundLayer.LineWidth = (nfloat)(_options.StrokeThickness > 0 ? _options.StrokeThickness : 1 / Math.Max(1, (double)TraitCollection.DisplayScale));
        Layer.ShadowPath = outline;
        CATransaction.Commit();
    }

    private CGRect GetBodyRect()
    {
        var bounds = Bounds;
        if (!_options.ShowArrow)
            return bounds;

        var arrow = (nfloat)ArrowLength;
        return Side switch
        {
            PopoverSide.Right => new CGRect(bounds.X + arrow, bounds.Y, bounds.Width - arrow, bounds.Height),
            PopoverSide.Left => new CGRect(bounds.X, bounds.Y, bounds.Width - arrow, bounds.Height),
            PopoverSide.Below => new CGRect(bounds.X, bounds.Y + arrow, bounds.Width, bounds.Height - arrow),
            _ => new CGRect(bounds.X, bounds.Y, bounds.Width, bounds.Height - arrow)
        };
    }

    /// <summary>
    /// Traces the body clockwise as a single path, splicing the arrow into the edge that faces
    /// the source, so the border has no seam where the arrow meets the body.
    /// </summary>
    private CGPath CreateOutline(CGRect body)
    {
        double minX = body.X, minY = body.Y, maxX = body.GetMaxX(), maxY = body.GetMaxY();
        var radius = Math.Clamp(_options.CornerRadius, 0, Math.Max(0, Math.Min(maxX - minX, maxY - minY) / 2));
        var arrow = _options.ShowArrow;

        var path = new CGPath();
        path.MoveToPoint(Point(minX + radius, minY));

        if (arrow && Side == PopoverSide.Below)
        {
            var x = ArrowTip(minX, maxX, radius);
            AddArrow(path, Point(x - ArrowHalfWidth, minY), Point(x, minY - ArrowLength), Point(x + ArrowHalfWidth, minY));
        }

        AddCorner(path, maxX, minY, maxX, maxY, radius);

        if (arrow && Side == PopoverSide.Left)
        {
            var y = ArrowTip(minY, maxY, radius);
            AddArrow(path, Point(maxX, y - ArrowHalfWidth), Point(maxX + ArrowLength, y), Point(maxX, y + ArrowHalfWidth));
        }

        AddCorner(path, maxX, maxY, minX, maxY, radius);

        if (arrow && Side == PopoverSide.Above)
        {
            var x = ArrowTip(minX, maxX, radius);
            AddArrow(path, Point(x + ArrowHalfWidth, maxY), Point(x, maxY + ArrowLength), Point(x - ArrowHalfWidth, maxY));
        }

        AddCorner(path, minX, maxY, minX, minY, radius);

        if (arrow && Side == PopoverSide.Right)
        {
            var y = ArrowTip(minY, maxY, radius);
            AddArrow(path, Point(minX, y + ArrowHalfWidth), Point(minX - ArrowLength, y), Point(minX, y - ArrowHalfWidth));
        }

        AddCorner(path, minX, minY, maxX, minY, radius);
        path.CloseSubpath();
        return path;
    }

    /// <summary>Keeps the arrow on the straight part of its edge, clear of the rounded corners.</summary>
    private double ArrowTip(double edgeStart, double edgeEnd, double radius)
    {
        var lowest = edgeStart + radius + ArrowHalfWidth;
        var highest = edgeEnd - radius - ArrowHalfWidth;
        return lowest > highest ? (edgeStart + edgeEnd) / 2 : Math.Clamp(ArrowPosition, lowest, highest);
    }

    private static void AddArrow(CGPath path, CGPoint start, CGPoint tip, CGPoint end)
    {
        path.AddLineToPoint(start);
        path.AddLineToPoint(tip);
        path.AddLineToPoint(end);
    }

    // A zero radius degrades to a line into the corner, i.e. a square corner.
    private static void AddCorner(CGPath path, double cornerX, double cornerY, double towardsX, double towardsY, double radius) =>
        path.AddArcToPoint((nfloat)cornerX, (nfloat)cornerY, (nfloat)towardsX, (nfloat)towardsY, (nfloat)radius);

    private static CGPoint Point(double x, double y) => new(x, y);
}

/// <summary>
/// Places the popover beside its source and dismisses it on an outside click.
/// </summary>
/// <remarks>
/// This replaces UIModalPresentationStyle.Popover, whose bubble shape (corner radius, arrow)
/// cannot be changed on Mac Catalyst.
/// </remarks>
internal sealed class PopoverPresentationController : UIPresentationController
{
    private readonly UIView _sourceView;
    private readonly PopoverDirection _direction;
    private readonly PopoverAlignment _alignment;
    private readonly PopoverChromeOptions _options;
    private readonly Action _onOutsideTap;

    private PopoverChromeView? _chrome;
    private UIView? _touchCatcher;

    public PopoverPresentationController(
        UIViewController presentedViewController,
        UIViewController? presentingViewController,
        UIView sourceView,
        PopoverDirection direction,
        PopoverAlignment alignment,
        PopoverChromeOptions options,
        Action onOutsideTap)
        : base(presentedViewController, presentingViewController)
    {
        _sourceView = sourceView;
        _direction = direction;
        _alignment = alignment;
        _options = options;
        _onOutsideTap = onOutsideTap;
    }

    /// <summary>The bubble wraps the content view; it is what the animator adds and moves.</summary>
    public override UIView PresentedView => _chrome ??= new PopoverChromeView(PresentedViewController.View!, _options);

    public override CGRect FrameOfPresentedViewInContainerView => ComputeLayout().Frame;

    public override void PresentationTransitionWillBegin()
    {
        base.PresentationTransitionWillBegin();

        if (ContainerView is not { } container)
            return;

        // Invisible, but it is what makes an outside click dismiss the popover.
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

    public override void ContainerViewWillLayoutSubviews()
    {
        base.ContainerViewWillLayoutSubviews();
        ApplyLayout();
    }

    /// <summary>Content that grows or shrinks while open (e.g. switching a mode) re-places the popover.</summary>
    public override void PreferredContentSizeDidChangeForChildContentContainer(IUIContentContainer container)
    {
        base.PreferredContentSizeDidChangeForChildContentContainer(container);
        ContainerView?.SetNeedsLayout();
    }

    internal void ApplyLayout()
    {
        if (_chrome is null || ContainerView is null)
            return;

        var (frame, side, arrowPosition) = ComputeLayout();
        _chrome.Side = side;
        _chrome.ArrowPosition = arrowPosition;

        double width = frame.Width, height = frame.Height;

        // Grow out of the point facing the source. Bounds and center are set rather than Frame,
        // which is undefined while the open/close animation has a scale transform applied.
        var anchor = side switch
        {
            PopoverSide.Right => new CGPoint(0, Fraction(arrowPosition, height)),
            PopoverSide.Left => new CGPoint(1, Fraction(arrowPosition, height)),
            PopoverSide.Below => new CGPoint(Fraction(arrowPosition, width), 0),
            _ => new CGPoint(Fraction(arrowPosition, width), 1)
        };

        _chrome.Layer.AnchorPoint = anchor;
        _chrome.Bounds = new CGRect(0, 0, width, height);
        _chrome.Center = new CGPoint(frame.X + anchor.X * width, frame.Y + anchor.Y * height);
        _chrome.SetNeedsLayout();
    }

    private (CGRect Frame, PopoverSide Side, double ArrowPosition) ComputeLayout()
    {
        if (ContainerView is not { } container)
            return (CGRect.Empty, PopoverSide.Below, 0);

        var source = _sourceView.ConvertRectToView(_sourceView.Bounds, container);
        return PopoverLayout.Compute(
            source,
            PopoverLayout.UsableArea(container),
            PresentedViewController.PreferredContentSize,
            _direction,
            _alignment,
            _options);
    }

    internal static double Fraction(double position, double length) =>
        length <= 0 ? 0.5 : Math.Clamp(position / length, 0, 1);
}

/// <summary>Where a popover goes: which side of its source, and where along that side.</summary>
internal static class PopoverLayout
{
    public const double EdgeMargin = 8;
    private const double GapWithArrow = 1;
    private const double GapWithoutArrow = 4;

    /// <summary>The part of <paramref name="container"/> a popover may cover: inside the safe area, off the edges.</summary>
    public static CGRect UsableArea(UIView container)
    {
        var insets = container.SafeAreaInsets;
        var left = insets.Left + EdgeMargin;
        var top = insets.Top + EdgeMargin;
        return new CGRect(
            left,
            top,
            Math.Max(0, container.Bounds.Width - insets.Right - EdgeMargin - left),
            Math.Max(0, container.Bounds.Height - insets.Bottom - EdgeMargin - top));
    }

    /// <summary>
    /// Places content of <paramref name="content"/> size beside <paramref name="source"/>, both in the
    /// coordinates of <paramref name="area"/>. The frame includes the arrow.
    /// </summary>
    public static (CGRect Frame, PopoverSide Side, double ArrowPosition) Compute(
        CGRect source,
        CGRect area,
        CGSize content,
        PopoverDirection direction,
        PopoverAlignment alignment,
        PopoverChromeOptions options)
    {
        double left = area.X, top = area.Y, right = area.GetMaxX(), bottom = area.GetMaxY();
        var arrow = options.ShowArrow ? PopoverChromeView.ArrowLength : 0;
        var gap = options.ShowArrow ? GapWithArrow : GapWithoutArrow;

        var side = ResolveSide(direction, source, left, top, right, bottom, content.Width + arrow + gap, content.Height + arrow + gap);
        var horizontal = side is PopoverSide.Left or PopoverSide.Right;

        var width = Math.Min(content.Width + (horizontal ? arrow : 0), Math.Max(0, right - left));
        var height = Math.Min(content.Height + (horizontal ? 0 : arrow), Math.Max(0, bottom - top));

        // With an edge alignment the arrow must still reach the source's middle, clear of the
        // rounded corner, so a small source pushes the popover back a little.
        var arrowInset = options.ShowArrow ? options.CornerRadius + PopoverChromeView.ArrowHalfWidth : 0;

        double x = side switch
        {
            PopoverSide.Right => source.GetMaxX() + gap,
            PopoverSide.Left => source.X - gap - width,
            _ => Align(alignment, source.X, source.GetMaxX(), width, arrowInset)
        };
        double y = side switch
        {
            PopoverSide.Below => source.GetMaxY() + gap,
            PopoverSide.Above => source.Y - gap - height,
            _ => Align(alignment, source.Y, source.GetMaxY(), height, arrowInset)
        };

        x = Math.Clamp(x, left, Math.Max(left, right - width));
        y = Math.Clamp(y, top, Math.Max(top, bottom - height));

        double arrowPosition = horizontal ? source.GetMidY() - y : source.GetMidX() - x;
        return (new CGRect(x, y, width, height), side, arrowPosition);
    }

    private static double Align(PopoverAlignment alignment, double sourceStart, double sourceEnd, double length, double arrowInset)
    {
        var middle = (sourceStart + sourceEnd) / 2;
        return alignment switch
        {
            PopoverAlignment.Start => Math.Min(sourceStart, middle - arrowInset),
            PopoverAlignment.End => Math.Max(sourceEnd, middle + arrowInset) - length,
            _ => middle - length / 2
        };
    }

    private static PopoverSide ResolveSide(PopoverDirection direction, CGRect source, double left, double top, double right, double bottom, double neededWidth, double neededHeight)
    {
        double spaceRight = right - source.GetMaxX();
        double spaceLeft = source.X - left;
        double spaceBelow = bottom - source.GetMaxY();
        double spaceAbove = source.Y - top;

        return direction switch
        {
            PopoverDirection.Right => Pick(PopoverSide.Right, spaceRight, PopoverSide.Left, spaceLeft, neededWidth),
            PopoverDirection.Left => Pick(PopoverSide.Left, spaceLeft, PopoverSide.Right, spaceRight, neededWidth),
            PopoverDirection.Up => Pick(PopoverSide.Above, spaceAbove, PopoverSide.Below, spaceBelow, neededHeight),
            PopoverDirection.Down => Pick(PopoverSide.Below, spaceBelow, PopoverSide.Above, spaceAbove, neededHeight),
            _ => spaceBelow >= neededHeight ? PopoverSide.Below
                : spaceAbove >= neededHeight ? PopoverSide.Above
                : spaceRight >= neededWidth ? PopoverSide.Right
                : spaceLeft >= neededWidth ? PopoverSide.Left
                : spaceBelow >= spaceAbove ? PopoverSide.Below : PopoverSide.Above
        };
    }

    /// <summary>The requested side when it fits, the opposite side when only that fits, else the roomier one.</summary>
    private static PopoverSide Pick(PopoverSide preferred, double preferredSpace, PopoverSide opposite, double oppositeSpace, double needed)
    {
        if (preferredSpace >= needed)
            return preferred;

        if (oppositeSpace >= needed)
            return opposite;

        return preferredSpace >= oppositeSpace ? preferred : opposite;
    }
}

/// <summary>Supplies the presentation controller and the open/close animations.</summary>
internal sealed class PopoverTransitioningDelegate : UIViewControllerTransitioningDelegate
{
    private readonly UIView _sourceView;
    private readonly PopoverDirection _direction;
    private readonly PopoverAlignment _alignment;
    private readonly PopoverChromeOptions _options;
    private readonly Action _onOutsideTap;

    public PopoverTransitioningDelegate(UIView sourceView, PopoverDirection direction, PopoverAlignment alignment, PopoverChromeOptions options, Action onOutsideTap)
    {
        _sourceView = sourceView;
        _direction = direction;
        _alignment = alignment;
        _options = options;
        _onOutsideTap = onOutsideTap;
    }

    public override UIPresentationController GetPresentationControllerForPresentedViewController(
        UIViewController presentedViewController,
        UIViewController? presentingViewController,
        UIViewController sourceViewController) =>
        new PopoverPresentationController(
            presentedViewController, presentingViewController, _sourceView, _direction, _alignment, _options, _onOutsideTap);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForPresentedController(
        UIViewController presented, UIViewController presenting, UIViewController source) =>
        new PopoverAnimator(presenting: true);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForDismissedController(
        UIViewController dismissed) =>
        new PopoverAnimator(presenting: false);
}

/// <summary>A short fade and scale out of the point facing the source, reversed on close.</summary>
internal sealed class PopoverAnimator : UIViewControllerAnimatedTransitioning
{
    private const float CollapsedScale = 0.96f;
    private const double PresentDuration = 0.12;
    private const double DismissDuration = 0.1;

    private readonly bool _presenting;

    public PopoverAnimator(bool presenting) => _presenting = presenting;

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
        var presentation = toController?.PresentationController as PopoverPresentationController;
        var toView = presentation?.PresentedView ?? context.GetViewFor(UITransitionContext.ToViewKey) ?? toController?.View;

        if (toController is null || toView is null)
        {
            context.CompleteTransition(true);
            return;
        }

        context.ContainerView.AddSubview(toView);

        if (presentation is not null)
            presentation.ApplyLayout();
        else
            toView.Frame = context.GetFinalFrameForViewController(toController);

        toView.LayoutIfNeeded();
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
        var fromController = context.GetViewControllerForKey(UITransitionContext.FromViewControllerKey);
        var fromView = (fromController?.PresentationController as PopoverPresentationController)?.PresentedView
                       ?? context.GetViewFor(UITransitionContext.FromViewKey)
                       ?? fromController?.View;

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
                fromView.Transform = CGAffineTransform.MakeIdentity();
                fromView.Alpha = 1;
                context.CompleteTransition(!context.TransitionWasCancelled);
            });
    }
}
