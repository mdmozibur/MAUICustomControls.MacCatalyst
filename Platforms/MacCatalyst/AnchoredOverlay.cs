using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>Why an <see cref="AnchoredOverlay"/> closed.</summary>
public enum OverlayCloseReason
{
    /// <summary><see cref="AnchoredOverlay.Close"/> was called.</summary>
    Programmatic,

    /// <summary>A click outside it, with light dismiss on.</summary>
    LightDismiss,

    /// <summary>Escape, while it had keyboard focus.</summary>
    Escape,
}

/// <summary>Where an overlay without an anchor sits in the window.</summary>
public enum OverlayWindowPosition
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

public sealed record AnchoredOverlayOptions
{
    /// <summary>The side of the anchor to open on; it flips when there is no room.</summary>
    public PopoverDirection Direction { get; init; } = PopoverDirection.Auto;

    public PopoverAlignment Alignment { get; init; } = PopoverAlignment.Center;

    /// <summary>Placement when there is no anchor.</summary>
    public OverlayWindowPosition WindowPosition { get; init; } = OverlayWindowPosition.BottomRight;

    /// <summary>
    /// Places the overlay's top-left corner at this offset from the anchor's top-left corner (or the
    /// window's, without an anchor) instead of beside the anchor, as a XAML Popup does.
    /// </summary>
    public Microsoft.Maui.Graphics.Point? Offset { get; init; }

    /// <summary>Draws the popover bubble (background, border, shadow). Off, the content draws itself.</summary>
    public bool ShowChrome { get; init; } = true;

    /// <summary>The arrow pointing at the anchor (only drawn beside an anchor).</summary>
    public bool ShowArrow { get; init; } = true;

    public double CornerRadius { get; init; } = 8;
    public Thickness Padding { get; init; } = new(12);
    public Microsoft.Maui.Graphics.Color? Stroke { get; init; }
    public double StrokeThickness { get; init; }

    public double MinWidth { get; init; }
    public double MaxWidth { get; init; } = double.PositiveInfinity;
    public double MaxHeight { get; init; } = double.PositiveInfinity;

    /// <summary>A click anywhere outside closes it (and does nothing else). Off, the app stays usable.</summary>
    public bool IsLightDismissEnabled { get; init; }

    /// <summary>Takes keyboard focus while open, so Escape closes it.</summary>
    public bool TakesKeyboardFocus { get; init; }

    /// <summary>VoiceOver's name for the overlay.</summary>
    public string? AccessibilityLabel { get; init; }

    /// <summary>Spoken by VoiceOver when it opens.</summary>
    public string? Announcement { get; init; }

    /// <summary>Fades and scales in and out; off, it appears and disappears at once (a XAML Popup).</summary>
    public bool Animated { get; init; } = true;
}

/// <summary>
/// A non-modal floating surface for MAUI content: a teaching tip or popup beside an element, or in a
/// corner of the window. Unlike a presented popover it leaves the rest of the window usable (unless
/// light dismiss is on) and follows its anchor while the layout moves.
/// </summary>
public sealed class AnchoredOverlay
{
    private const float CollapsedScale = 0.96f;

    private readonly IView _content;
    private readonly UIView? _anchor;
    private readonly AnchoredOverlayOptions _options;
    private readonly PopoverChromeOptions _chromeOptions;
    private readonly OverlayContainerView _container;
    private readonly DialogContentView _host;
    private readonly UIView _surface;
    private readonly PopoverChromeView? _chrome;
    private CADisplayLink? _tracker;
    private CGRect _lastAnchorRect = CGRect.Empty;
    private Microsoft.Maui.Graphics.Point? _offset;
    private bool _closed;

    private AnchoredOverlay(IView content, IMauiContext mauiContext, UIView? anchor, UIWindow window, AnchoredOverlayOptions options)
    {
        _content = content;
        _anchor = anchor;
        _options = options;
        _offset = options.Offset;
        _chromeOptions = new PopoverChromeOptions(
            options.CornerRadius,
            options.ShowArrow && anchor is not null && options.Offset is null,
            options.ShowChrome ? options.Padding : new Thickness(0),
            options.Stroke?.ToPlatform(),
            options.StrokeThickness);

        _container = new OverlayContainerView(this)
        {
            Frame = window.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
        };

        if (options.IsLightDismissEnabled)
        {
            _container.AddSubview(new DismissCatcherView(() => Close(OverlayCloseReason.LightDismiss))
            {
                Frame = _container.Bounds,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            });
        }

        _host = new DialogContentView(content, content.ToPlatform(mauiContext));
        if (options.ShowChrome)
        {
            _chrome = new PopoverChromeView(_host, _chromeOptions);
            _surface = _chrome;
        }
        else
        {
            _surface = _host;
        }

        _surface.AccessibilityLabel = options.AccessibilityLabel;
        _container.AddSubview(_surface);
        window.AddSubview(_container);
    }

    public bool IsOpen => !_closed;

    /// <summary>Raised once, after the overlay has left the screen.</summary>
    public event Action<OverlayCloseReason>? Closed;

    /// <summary>
    /// Shows <paramref name="content"/> beside <paramref name="anchor"/> (a platform view in a window),
    /// or in the key window when there is none. Returns null when no window is available.
    /// </summary>
    public static AnchoredOverlay? Show(IView content, IMauiContext mauiContext, UIView? anchor, AnchoredOverlayOptions options)
    {
        var window = anchor?.Window ?? PresentationHelpers.GetTopViewController()?.View?.Window;
        if (window is null)
        {
            return null;
        }

        var overlay = new AnchoredOverlay(content, mauiContext, anchor, window, options);
        overlay.Open();
        return overlay;
    }

    /// <summary>Re-measures the content and re-places the overlay (after its content changed).</summary>
    public void SetNeedsLayout()
    {
        if (!_closed)
        {
            _container.SetNeedsLayout();
        }
    }

    public void Close() => Close(OverlayCloseReason.Programmatic);

    /// <summary>Moves an overlay opened with an <see cref="AnchoredOverlayOptions.Offset"/> (a popup's offsets changed).</summary>
    public void SetOffset(Microsoft.Maui.Graphics.Point offset)
    {
        if (_offset is null || _offset == offset)
        {
            return;
        }

        _offset = offset;
        SetNeedsLayout();
    }

    private void Open()
    {
        _container.LayoutIfNeeded();
        if (_options.Animated)
        {
            _surface.Alpha = 0;
            _surface.Transform = CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);
            UIView.Animate(0.12, 0, UIViewAnimationOptions.CurveEaseOut, () =>
            {
                _surface.Alpha = 1;
                _surface.Transform = CGAffineTransform.MakeIdentity();
            }, null);
        }

        if (_anchor is not null)
        {
            // Follow the anchor when something else moves it (a pane resizing, a list scrolling).
            // Cheap: it only compares one rectangle per tick, and runs only while the overlay is open.
            _tracker = CADisplayLink.Create(TrackAnchor);
            _tracker.PreferredFramesPerSecond = 15;
            _tracker.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }

        if (_options.TakesKeyboardFocus)
        {
            _container.BecomeFirstResponder();
        }

        if (!string.IsNullOrWhiteSpace(_options.Announcement))
        {
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, new NSString(_options.Announcement));
        }

        UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, _surface);
    }

    internal void Close(OverlayCloseReason reason)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _tracker?.Invalidate();
        _tracker = null;
        if (_container.IsFirstResponder)
        {
            _container.ResignFirstResponder();
        }

        // Clicks already pass through while it fades.
        _container.UserInteractionEnabled = false;
        void Finish()
        {
            _container.RemoveFromSuperview();
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, null);
            Closed?.Invoke(reason);
        }

        if (!_options.Animated)
        {
            Finish();
            return;
        }

        UIView.Animate(0.1, 0, UIViewAnimationOptions.CurveEaseIn, () =>
        {
            _surface.Alpha = 0;
            _surface.Transform = CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);
        }, Finish);
    }

    private void TrackAnchor()
    {
        var rect = AnchorRect();
        if (rect != _lastAnchorRect)
        {
            _container.SetNeedsLayout();
        }
    }

    private CGRect AnchorRect() =>
        _anchor?.Window is not null ? _anchor.ConvertRectToView(_anchor.Bounds, _container) : CGRect.Empty;

    internal void ApplyLayout()
    {
        // An anchor that left the window (its page navigated away) takes the overlay with it until it returns.
        var anchorGone = _anchor is not null && _anchor.Window is null;
        _surface.Hidden = anchorGone;
        if (anchorGone)
        {
            return;
        }

        var area = PopoverLayout.UsableArea(_container);
        var padding = _chromeOptions.Padding;
        var arrow = _chromeOptions.ShowArrow ? PopoverChromeView.ArrowLength : 0;

        var maxWidth = Math.Max(0, Math.Min(_options.MaxWidth, area.Width - arrow) - padding.HorizontalThickness);
        var maxHeight = Math.Max(0, Math.Min(_options.MaxHeight, area.Height - arrow) - padding.VerticalThickness);
        var minWidth = Math.Min(Math.Max(0, _options.MinWidth - padding.HorizontalThickness), maxWidth);

        var desired = _content.Measure(maxWidth, double.PositiveInfinity);
        var width = Math.Clamp(desired.Width, minWidth, maxWidth);
        var height = Math.Min(_content.Measure(width, double.PositiveInfinity).Height, maxHeight);
        var size = new CGSize(width + padding.HorizontalThickness, height + padding.VerticalThickness);

        _lastAnchorRect = AnchorRect();

        CGRect frame;
        var side = PopoverSide.Below;
        double arrowPosition = 0;

        if (_offset is { } offset)
        {
            var origin = _anchor is null ? CGPoint.Empty : _lastAnchorRect.Location;
            frame = Constrain(new CGRect(origin.X + offset.X, origin.Y + offset.Y, size.Width, size.Height), area);
        }
        else if (_anchor is not null)
        {
            (frame, side, arrowPosition) = PopoverLayout.Compute(_lastAnchorRect, area, size, _options.Direction, _options.Alignment, _chromeOptions);
        }
        else
        {
            frame = PlaceInWindow(size, area, _options.WindowPosition);
        }

        if (_chrome is not null)
        {
            _chrome.Side = side;
            _chrome.ArrowPosition = arrowPosition;
        }

        // Bounds and center rather than Frame, which is undefined while the open/close scale runs.
        var anchorPoint = !_chromeOptions.ShowArrow ? new CGPoint(0.5, 0.5) : side switch
        {
            PopoverSide.Right => new CGPoint(0, PopoverPresentationController.Fraction(arrowPosition, frame.Height)),
            PopoverSide.Left => new CGPoint(1, PopoverPresentationController.Fraction(arrowPosition, frame.Height)),
            PopoverSide.Below => new CGPoint(PopoverPresentationController.Fraction(arrowPosition, frame.Width), 0),
            _ => new CGPoint(PopoverPresentationController.Fraction(arrowPosition, frame.Width), 1)
        };

        _surface.Layer.AnchorPoint = anchorPoint;
        _surface.Bounds = new CGRect(0, 0, frame.Width, frame.Height);
        _surface.Center = new CGPoint(frame.X + anchorPoint.X * frame.Width, frame.Y + anchorPoint.Y * frame.Height);
        _surface.SetNeedsLayout();
    }

    private static CGRect PlaceInWindow(CGSize size, CGRect area, OverlayWindowPosition position)
    {
        var column = (int)position % 3;
        var row = (int)position / 3;
        var x = column switch { 0 => area.X, 1 => area.GetMidX() - size.Width / 2, _ => area.GetMaxX() - size.Width };
        var y = row switch { 0 => area.Y, 1 => area.GetMidY() - size.Height / 2, _ => area.GetMaxY() - size.Height };
        return Constrain(new CGRect(x, y, size.Width, size.Height), area);
    }

    private static CGRect Constrain(CGRect frame, CGRect area)
    {
        var x = Math.Clamp((double)frame.X, (double)area.X, Math.Max((double)area.X, (double)(area.GetMaxX() - frame.Width)));
        var y = Math.Clamp((double)frame.Y, (double)area.Y, Math.Max((double)area.Y, (double)(area.GetMaxY() - frame.Height)));
        return new CGRect(x, y, frame.Width, frame.Height);
    }

    /// <summary>Covers the window but lets every click through that misses the overlay itself.</summary>
    private sealed class OverlayContainerView : UIView
    {
        private readonly WeakReference<AnchoredOverlay> _owner;

        public OverlayContainerView(AnchoredOverlay owner)
        {
            _owner = new WeakReference<AnchoredOverlay>(owner);
            BackgroundColor = UIColor.Clear;
        }

        public override UIView? HitTest(CGPoint point, UIEvent? uievent)
        {
            var hit = base.HitTest(point, uievent);
            return ReferenceEquals(hit, this) ? null : hit;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (_owner.TryGetTarget(out var owner))
            {
                owner.ApplyLayout();
            }
        }

        public override bool CanBecomeFirstResponder => true;

        public override UIKeyCommand[] KeyCommands =>
        [
            UIKeyCommand.Create(UIKeyCommand.Escape, (UIKeyModifierFlags)0, new Selector("overlayEscape:"))
        ];

        [Export("overlayEscape:")]
        public void OverlayEscape(UIKeyCommand command)
        {
            if (_owner.TryGetTarget(out var owner))
            {
                owner.Close(OverlayCloseReason.Escape);
            }
        }
    }

    /// <summary>Closes the overlay on the first press anywhere outside it, and swallows that press.</summary>
    private sealed class DismissCatcherView : UIView
    {
        private readonly Action _dismiss;

        public DismissCatcherView(Action dismiss)
        {
            _dismiss = dismiss;
            BackgroundColor = UIColor.Clear;
        }

        public override void TouchesBegan(NSSet touches, UIEvent? evt) => _dismiss();

        public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt) => _dismiss();
    }
}
