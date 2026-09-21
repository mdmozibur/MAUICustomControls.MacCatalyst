using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ProgressRingHandler : ViewHandler<ProgressRing, ProgressRingView>
{
    // WinUI's default ProgressRing size when no Width/Height is given.
    private const double DefaultSize = 32;

    public static readonly PropertyMapper<ProgressRing, ProgressRingHandler> Mapper = new(ViewMapper)
    {
        [nameof(ProgressRing.IsActive)] = MapState,
        [nameof(ProgressRing.IsIndeterminate)] = MapState,
        [nameof(ProgressRing.Value)] = MapState,
        [nameof(ProgressRing.Minimum)] = MapState,
        [nameof(ProgressRing.Maximum)] = MapState,
        [nameof(ProgressRing.TextColor)] = MapState,
        [nameof(ProgressRing.TrackColor)] = MapState,
    };

    public ProgressRingHandler() : base(Mapper)
    {
    }

    protected override ProgressRingView CreatePlatformView() => new();

    public override Microsoft.Maui.Graphics.Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        var width = VirtualView.WidthRequest >= 0 ? VirtualView.WidthRequest : DefaultSize;
        var height = VirtualView.HeightRequest >= 0 ? VirtualView.HeightRequest : DefaultSize;
        return new Microsoft.Maui.Graphics.Size(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));
    }

    private static void MapState(ProgressRingHandler handler, ProgressRing ring)
    {
        handler.PlatformView.Update(
            ring.IsActive,
            ring.IsIndeterminate,
            ring.Fraction,
            ring.TextColor?.ToPlatform(),
            ring.TrackColor?.ToPlatform());
    }
}

public sealed class ProgressRingView : UIView
{
    private const string SpinKey = "spin";
    private const string StrokeKey = "stroke";

    private readonly CAShapeLayer _track = new() { FillColor = UIColor.Clear.CGColor, LineCap = CAShapeLayer.CapRound };
    private readonly CAShapeLayer _arc = new() { FillColor = UIColor.Clear.CGColor, LineCap = CAShapeLayer.CapRound };
    private bool _isActive = true;
    private bool _isIndeterminate = true;
    private double _fraction;
    private UIColor? _arcColor;
    private UIColor? _trackColor;

    public ProgressRingView()
    {
        BackgroundColor = UIColor.Clear;
        UserInteractionEnabled = false;
        Layer.AddSublayer(_track);
        Layer.AddSublayer(_arc);
        IsAccessibilityElement = true;
        AccessibilityTraits = UIAccessibilityTrait.UpdatesFrequently;
    }

    public void Update(bool isActive, bool isIndeterminate, double fraction, UIColor? arcColor, UIColor? trackColor)
    {
        var restart = isIndeterminate != _isIndeterminate || isActive != _isActive;
        _isActive = isActive;
        _isIndeterminate = isIndeterminate;
        _fraction = fraction;
        _arcColor = arcColor;
        _trackColor = trackColor;

        ApplyColors();
        // UIView.Hidden belongs to MAUI's IsVisible mapping; an inactive ring keeps its slot and
        // simply draws nothing, as in WinUI.
        _track.Hidden = !isActive;
        _arc.Hidden = !isActive;
        IsAccessibilityElement = isActive;
        AccessibilityLabel = isIndeterminate ? "In progress" : $"{Math.Round(fraction * 100)}%";

        CATransaction.Begin();
        CATransaction.DisableActions = true;
        if (!isIndeterminate)
        {
            _arc.StrokeStart = 0;
            _arc.StrokeEnd = (nfloat)fraction;
        }
        CATransaction.Commit();

        if (restart)
        {
            UpdateAnimations();
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var size = (double)Math.Min(Bounds.Width, Bounds.Height);
        var lineWidth = (nfloat)Math.Clamp(size * 0.1, 1.5, 6);
        var radius = (nfloat)((size - lineWidth) / 2);
        var center = new CGPoint(Bounds.GetMidX(), Bounds.GetMidY());

        // Start at 12 o'clock and run clockwise, like the WinUI ring.
        var path = UIBezierPath.FromArc(center, radius, -(nfloat)Math.PI / 2, (nfloat)(Math.PI * 1.5), true);
        foreach (var layer in new[] { _track, _arc })
        {
            layer.Frame = Bounds;
            layer.Path = path.CGPath;
            layer.LineWidth = lineWidth;
        }
    }

    public override void MovedToWindow()
    {
        base.MovedToWindow();
        // Core Animation drops running animations when a layer leaves the window.
        UpdateAnimations();
    }

    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        // CGColors are resolved once; re-resolve the dynamic system colours for light/dark.
        ApplyColors();
    }

    private void ApplyColors()
    {
        var traits = TraitCollection;
        _arc.StrokeColor = (_arcColor ?? TintColor ?? UIColor.SystemBlue).GetResolvedColor(traits).CGColor;
        var track = _isIndeterminate ? UIColor.Clear : _trackColor ?? UIColor.SystemFill;
        _track.StrokeColor = track.GetResolvedColor(traits).CGColor;
    }

    public override void TintColorDidChange()
    {
        base.TintColorDidChange();
        ApplyColors();
    }

    private void UpdateAnimations()
    {
        _arc.RemoveAnimation(SpinKey);
        _arc.RemoveAnimation(StrokeKey);
        _arc.Transform = CATransform3D.Identity;

        if (!_isActive || !_isIndeterminate || Window is null)
        {
            if (!_isIndeterminate)
            {
                _arc.StrokeStart = 0;
                _arc.StrokeEnd = (nfloat)_fraction;
            }

            return;
        }

        // An arc whose head and tail chase each other while the whole ring turns.
        var spin = CABasicAnimation.FromKeyPath("transform.rotation.z");
        spin.From = Foundation.NSNumber.FromDouble(0);
        spin.To = Foundation.NSNumber.FromDouble(Math.PI * 2);
        spin.Duration = 2;
        spin.RepeatCount = float.PositiveInfinity;
        _arc.AddAnimation(spin, SpinKey);

        var head = CABasicAnimation.FromKeyPath("strokeEnd");
        head.From = Foundation.NSNumber.FromDouble(0);
        head.To = Foundation.NSNumber.FromDouble(1);
        head.Duration = 1;
        head.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseInEaseOut);

        var tail = CABasicAnimation.FromKeyPath("strokeStart");
        tail.From = Foundation.NSNumber.FromDouble(0);
        tail.To = Foundation.NSNumber.FromDouble(1);
        tail.BeginTime = 0.5;
        tail.Duration = 1;
        tail.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseInEaseOut);

        var group = new CAAnimationGroup
        {
            Animations = [head, tail],
            Duration = 1.5,
            RepeatCount = float.PositiveInfinity,
        };
        _arc.StrokeStart = 0;
        _arc.StrokeEnd = 1;
        _arc.AddAnimation(group, StrokeKey);
    }
}
