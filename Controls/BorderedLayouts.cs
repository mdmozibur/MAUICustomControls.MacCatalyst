using System;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
#if MACCATALYST
using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;
#endif

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// Shared native-chrome rendering for the bordered layout subclasses.
/// </summary>
/// <remarks>
/// MAUI layouts (StackLayout, Grid) cannot draw a border, unlike the UWP panels they map from.
/// Rather than forcing converted code onto a wrapping Border (which would break every subsequent
/// Children/Measure/positioning call against the layout variable), the bordered subclasses keep the
/// element a real layout and paint the UWP-style BorderBrush/BorderThickness/CornerRadius onto the
/// native layer, exactly as TitledRegion does for its chrome.
/// </remarks>
internal static class BorderedLayoutChrome
{
#if MACCATALYST
    private const string BorderLayerName = "UwpPanelBorder";
#endif

    public static void Apply(VisualElement element, Brush? borderBrush, Thickness borderThickness, CornerRadius cornerRadius)
    {
#if MACCATALYST
        if (element.Handler?.PlatformView is not UIView platformView)
        {
            return;
        }

        var layer = platformView.Layer;
        var radius = Math.Max(
            Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
            Math.Max(cornerRadius.BottomLeft, cornerRadius.BottomRight));

        // UWP rounds the background and the border, not the children, so nothing is clipped.
        // Corners without a radius stay square (a segmented "4,0,0,4" panel).
        layer.CornerRadius = (nfloat)Math.Max(0, radius);
        layer.MaskedCorners = radius > 0 ? GetRoundedCorners(cornerRadius) : AllCorners;
        layer.MasksToBounds = false;

        var color = (borderBrush as SolidColorBrush)?.Color;
        var borderLayer = layer.Sublayers?.OfType<CAShapeLayer>().FirstOrDefault(sublayer => sublayer.Name == BorderLayerName);
        var hasBorder = color is not null &&
            (borderThickness.Left > 0 || borderThickness.Top > 0 || borderThickness.Right > 0 || borderThickness.Bottom > 0);

        if (!hasBorder || IsUniform(borderThickness))
        {
            // One width all round: the layer's own border, which follows the corner radius.
            borderLayer?.RemoveFromSuperLayer();
            layer.BorderWidth = hasBorder ? (nfloat)borderThickness.Left : 0;
            layer.BorderColor = hasBorder ? color!.ToCGColor() : null;
            return;
        }

        // Sides of different widths (a "0,0,0,1" separator): the ring between the bounds and the
        // bounds inset by each side's width, filled even-odd.
        layer.BorderWidth = 0;
        if (borderLayer is null)
        {
            borderLayer = new CAShapeLayer { Name = BorderLayerName, FillRule = CAShapeLayer.FillRuleEvenOdd };
            layer.AddSublayer(borderLayer);
        }

        var bounds = platformView.Bounds;
        var inner = new CGRect(
            bounds.X + borderThickness.Left,
            bounds.Y + borderThickness.Top,
            Math.Max(0, bounds.Width - borderThickness.Left - borderThickness.Right),
            Math.Max(0, bounds.Height - borderThickness.Top - borderThickness.Bottom));
        var path = UIBezierPath.FromRoundedRect(bounds, (nfloat)Math.Max(0, radius));
        path.AppendPath(UIBezierPath.FromRoundedRect(inner, (nfloat)Math.Max(0, radius - Math.Max(borderThickness.Left, borderThickness.Top))));
        borderLayer.Frame = bounds;
        borderLayer.Path = path.CGPath;
        borderLayer.FillColor = color!.ToCGColor();
        borderLayer.ZPosition = 1;
#endif
    }

#if MACCATALYST
    private const CACornerMask AllCorners =
        CACornerMask.MinXMinYCorner | CACornerMask.MaxXMinYCorner | CACornerMask.MinXMaxYCorner | CACornerMask.MaxXMaxYCorner;

    private static bool IsUniform(Thickness thickness) =>
        thickness.Left == thickness.Top && thickness.Top == thickness.Right && thickness.Right == thickness.Bottom;

    private static CACornerMask GetRoundedCorners(CornerRadius radius)
    {
        var corners = (CACornerMask)0;
        if (radius.TopLeft > 0) corners |= CACornerMask.MinXMinYCorner;
        if (radius.TopRight > 0) corners |= CACornerMask.MaxXMinYCorner;
        if (radius.BottomRight > 0) corners |= CACornerMask.MaxXMaxYCorner;
        if (radius.BottomLeft > 0) corners |= CACornerMask.MinXMaxYCorner;
        return corners;
    }
#endif
}

/// <summary>
/// StackLayout that renders a UWP-style border. Drop-in replacement for a UWP StackPanel that set
/// BorderBrush / BorderThickness / CornerRadius.
/// </summary>
public class BorderedStackLayout : StackLayout
{
    public static readonly BindableProperty BorderBrushProperty = BindableProperty.Create(
        nameof(BorderBrush), typeof(Brush), typeof(BorderedStackLayout), null, propertyChanged: OnChromeChanged);

    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), typeof(Thickness), typeof(BorderedStackLayout), default(Thickness), propertyChanged: OnChromeChanged);

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(CornerRadius), typeof(BorderedStackLayout), default(CornerRadius), propertyChanged: OnChromeChanged);

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public BorderedStackLayout()
    {
        HandlerChanged += (_, _) => ApplyChrome();
        // A border whose sides differ is a path sized to the panel.
        SizeChanged += (_, _) => ApplyChrome();
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        _ = oldValue;
        _ = newValue;
        ((BorderedStackLayout)bindable).ApplyChrome();
    }

    private void ApplyChrome() => BorderedLayoutChrome.Apply(this, BorderBrush, BorderThickness, CornerRadius);
}

/// <summary>
/// Grid that renders a UWP-style border. Drop-in replacement for a UWP Grid that set
/// BorderBrush / BorderThickness / CornerRadius.
/// </summary>
public class BorderedGrid : Grid
{
    public static readonly BindableProperty BorderBrushProperty = BindableProperty.Create(
        nameof(BorderBrush), typeof(Brush), typeof(BorderedGrid), null, propertyChanged: OnChromeChanged);

    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), typeof(Thickness), typeof(BorderedGrid), default(Thickness), propertyChanged: OnChromeChanged);

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(CornerRadius), typeof(BorderedGrid), default(CornerRadius), propertyChanged: OnChromeChanged);

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public BorderedGrid()
    {
        HandlerChanged += (_, _) => ApplyChrome();
        // A border whose sides differ is a path sized to the panel.
        SizeChanged += (_, _) => ApplyChrome();
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        _ = oldValue;
        _ = newValue;
        ((BorderedGrid)bindable).ApplyChrome();
    }

    private void ApplyChrome() => BorderedLayoutChrome.Apply(this, BorderBrush, BorderThickness, CornerRadius);
}
