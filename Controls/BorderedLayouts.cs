using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
#if MACCATALYST
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
    public static void Apply(VisualElement element, Brush? borderBrush, Thickness borderThickness, CornerRadius cornerRadius)
    {
#if MACCATALYST
        if (element.Handler?.PlatformView is not UIView platformView)
        {
            return;
        }

        var thickness = Math.Max(
            Math.Max(borderThickness.Left, borderThickness.Top),
            Math.Max(borderThickness.Right, borderThickness.Bottom));
        var radius = Math.Max(
            Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
            Math.Max(cornerRadius.BottomLeft, cornerRadius.BottomRight));

        platformView.Layer.BorderWidth = (nfloat)Math.Max(0, thickness);
        platformView.Layer.BorderColor = borderBrush is SolidColorBrush solidBrush
            ? solidBrush.Color.ToCGColor()
            : Colors.Transparent.ToCGColor();
        platformView.Layer.CornerRadius = (nfloat)Math.Max(0, radius);
        platformView.Layer.MasksToBounds = radius > 0;
#endif
    }
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
    }

    private static void OnChromeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        _ = oldValue;
        _ = newValue;
        ((BorderedGrid)bindable).ApplyChrome();
    }

    private void ApplyChrome() => BorderedLayoutChrome.Apply(this, BorderBrush, BorderThickness, CornerRadius);
}
