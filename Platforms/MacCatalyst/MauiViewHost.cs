using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Hosts a templated MAUI view inside a plain <see cref="UIView"/>.
/// </summary>
/// <remarks>
/// This is the bridge that lets item templates stay in XAML while the drop-down
/// itself is UIKit. MAUI's platform views do not publish an intrinsic content size —
/// they size through the cross-platform measure pass — so measurement is forwarded
/// to <c>IView.Measure</c> rather than left to Auto Layout.
/// </remarks>
internal sealed class MauiViewHost : UIView
{
    private readonly IView _view;
    private readonly UIView _platformView;

    /// <summary>Width used when Auto Layout asks for an intrinsic size.</summary>
    public double LayoutWidth { get; set; }

    public MauiViewHost(IView view, IMauiContext mauiContext)
    {
        _view = view;
        _platformView = view.ToPlatform(mauiContext);

        // The templated view is presentation only. Left interactive, the MAUI platform
        // views sit in front of the row and swallow the click everywhere they cover,
        // so only the padding around them would select the item.
        UserInteractionEnabled = false;

        AddSubview(_platformView);
    }

    public override CGSize SizeThatFits(CGSize size)
    {
        var width = size.Width > 0 && !double.IsInfinity(size.Width) ? size.Width : LayoutWidth;
        var measured = _view.Measure(width <= 0 ? double.PositiveInfinity : width, double.PositiveInfinity);
        return new CGSize(measured.Width, measured.Height);
    }

    /// <summary>The view's width when nothing constrains it: a text row on a single line.</summary>
    public double NaturalWidth => _view.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;

    public override CGSize IntrinsicContentSize =>
        SizeThatFits(new CGSize(LayoutWidth <= 0 ? NoIntrinsicMetric : LayoutWidth, NoIntrinsicMetric));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        // Arrange must run before the platform frame is set, so children are positioned.
        _view.Arrange(new Rect(0, 0, Bounds.Width, Bounds.Height));
        _platformView.Frame = Bounds;
    }
}
