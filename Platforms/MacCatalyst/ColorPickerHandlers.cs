using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>Maps <see cref="ColorSpectrum"/> to <see cref="ColorSpectrumView"/>.</summary>
public sealed class ColorSpectrumHandler : ViewHandler<ColorSpectrum, ColorSpectrumView>
{
    public static readonly PropertyMapper<ColorSpectrum, ColorSpectrumHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ColorSpectrum.Hue)] = MapSelection,
        [nameof(ColorSpectrum.Saturation)] = MapSelection,
        [nameof(ColorSpectrum.Value)] = MapSelection,
        [nameof(ColorSpectrum.CornerRadius)] = MapCornerRadius,
    };

    public ColorSpectrumHandler() : base(PropertyMapper)
    {
    }

    protected override ColorSpectrumView CreatePlatformView() => new();

    protected override void ConnectHandler(ColorSpectrumView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SelectedByUser = (hue, saturation) => VirtualView?.SelectFromUser(hue, saturation);
    }

    protected override void DisconnectHandler(ColorSpectrumView platformView)
    {
        platformView.SelectedByUser = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapSelection(ColorSpectrumHandler handler, ColorSpectrum spectrum) =>
        handler.PlatformView.Show(spectrum.Hue, spectrum.Saturation, spectrum.Value);

    public static void MapCornerRadius(ColorSpectrumHandler handler, ColorSpectrum spectrum) =>
        handler.PlatformView.CornerRadius = (nfloat)spectrum.CornerRadius;
}

/// <summary>Maps <see cref="ColorPickerSlider"/> to <see cref="ColorPickerSliderView"/>.</summary>
public sealed class ColorPickerSliderHandler : ViewHandler<ColorPickerSlider, ColorPickerSliderView>
{
    public static readonly PropertyMapper<ColorPickerSlider, ColorPickerSliderHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ColorPickerSlider.Hue)] = MapColor,
        [nameof(ColorPickerSlider.Saturation)] = MapColor,
        [nameof(ColorPickerSlider.Value)] = MapColor,
    };

    public ColorPickerSliderHandler() : base(PropertyMapper)
    {
    }

    protected override ColorPickerSliderView CreatePlatformView() => new();

    protected override void ConnectHandler(ColorPickerSliderView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ValueSetByUser = value => VirtualView?.SetValueFromUser(value);
    }

    protected override void DisconnectHandler(ColorPickerSliderView platformView)
    {
        platformView.ValueSetByUser = null;
        base.DisconnectHandler(platformView);
    }

    public static void MapColor(ColorPickerSliderHandler handler, ColorPickerSlider slider) =>
        handler.PlatformView.Show(slider.Hue, slider.Saturation, slider.Value);
}

/// <summary>
/// The hue-by-saturation box of WinUI's ColorSpectrum: hue runs left to right, saturation from
/// full at the top to none at the bottom, and the value darkens the whole field.
/// </summary>
/// <remarks>
/// The field at full value is one image, made once for the process. A black layer over it carries
/// the value as its opacity, and the selection is a ring layer, so neither a drag in the field nor
/// a move of the value slider draws anything again.
/// </remarks>
public sealed class ColorSpectrumView : UIView
{
    // WinUI's SelectionEllipsePanel is 16 x 16 with a 2 px stroke; its FocusEllipse sits 2 px outside.
    private const float RingSize = 16;
    private const float RingStroke = 2;
    private const int ImageSize = 256;

    private static CGImage? s_field;

    private readonly CALayer _clip = new();
    private readonly CALayer _field = new();
    private readonly CALayer _shade = new();
    private readonly CAShapeLayer _ring = new();
    private readonly CAShapeLayer _focusRing = new();

    private double _hue;
    private double _saturation;
    private double _value = 1;

    public ColorSpectrumView()
    {
        _field.Contents = s_field ??= CreateField();
        _field.ContentsGravity = CALayer.GravityResize;
        _shade.BackgroundColor = UIColor.Black.CGColor;
        _clip.MasksToBounds = true;
        _clip.CornerRadius = 4;
        _clip.BorderWidth = 1;
        _clip.AddSublayer(_field);
        _clip.AddSublayer(_shade);
        Layer.AddSublayer(_clip);

        foreach (var ring in new[] { _focusRing, _ring })
        {
            ring.FillColor = UIColor.Clear.CGColor;
            ring.LineWidth = RingStroke;
            Layer.AddSublayer(ring);
        }

        _ring.Path = UIBezierPath.FromOval(new CGRect(RingStroke / 2, RingStroke / 2, RingSize - RingStroke, RingSize - RingStroke)).CGPath;
        _focusRing.Path = UIBezierPath.FromOval(new CGRect(-RingStroke / 2, -RingStroke / 2, RingSize + RingStroke, RingSize + RingStroke)).CGPath;
        _focusRing.Hidden = true;

        IsAccessibilityElement = true;
        AccessibilityTraits = UIAccessibilityTrait.Adjustable;
        AccessibilityLabel = "Color spectrum";
        UpdateBorder();
    }

    /// <summary>The user chose hue (0 to 360) and saturation (0 to 1) with the pointer or the arrow keys.</summary>
    public Action<double, double>? SelectedByUser { get; set; }

    public nfloat CornerRadius
    {
        get => _clip.CornerRadius;
        set => _clip.CornerRadius = value;
    }

    public void Show(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = saturation;
        _value = value;
        UpdateLayers();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        UpdateLayers();
    }

    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        UpdateBorder();
    }

    // WinUI's ColorPickerBorderStyle: a thin low-contrast outline.
    private void UpdateBorder() => _clip.BorderColor = UIColor.Label.ColorWithAlpha(0.2f).CGColor;

    private void UpdateLayers()
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // No implicit animations: the ring follows the pointer, the shade the slider.
        CATransaction.Begin();
        CATransaction.DisableActions = true;
        _clip.Frame = bounds;
        _field.Frame = bounds;
        _shade.Frame = bounds;
        _shade.Opacity = (float)(1 - _value);

        var x = _hue / 360 * bounds.Width;
        var y = (1 - _saturation) * bounds.Height;
        var ringFrame = new CGRect(x - (RingSize / 2), y - (RingSize / 2), RingSize, RingSize);
        _ring.Frame = ringFrame;
        _focusRing.Frame = ringFrame;

        // WinUI's SelectionEllipseLight/Dark: a white ring on a dark colour, a black one on a light colour.
        ColorPicker.HsvToRgb(_hue, _saturation, _value, out var red, out var green, out var blue);
        var isLightColor = ((0.299 * red) + (0.587 * green) + (0.114 * blue)) / 255 > 0.5;
        _ring.StrokeColor = (isLightColor ? UIColor.Black : UIColor.White).CGColor;
        _focusRing.StrokeColor = (isLightColor ? UIColor.White : UIColor.Black).CGColor;
        CATransaction.Commit();

        AccessibilityValue = $"Hue {Math.Round(_hue)}, saturation {Math.Round(_saturation * 100)}%";
    }

    public override bool CanBecomeFirstResponder => true;

    public override bool BecomeFirstResponder()
    {
        var became = base.BecomeFirstResponder();
        _focusRing.Hidden = !became;
        return became;
    }

    public override bool ResignFirstResponder()
    {
        var resigned = base.ResignFirstResponder();
        _focusRing.Hidden = resigned || _focusRing.Hidden;
        return resigned;
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        BecomeFirstResponder();
        // A click is not keyboard focus: WinUI shows the focus ring for the keyboard only.
        _focusRing.Hidden = true;
        SelectAt(touches);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt) => SelectAt(touches);

    public override void TouchesEnded(NSSet touches, UIEvent? evt) => SelectAt(touches);

    private void SelectAt(NSSet touches)
    {
        if (touches.AnyObject is UITouch touch)
        {
            SelectAt(touch.LocationInView(this));
        }
    }

    /// <summary>Selects the colour at a point of the field (clamped to it), as a click there does.</summary>
    internal void SelectAt(CGPoint point)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var hue = Math.Clamp(point.X / bounds.Width, 0, 1) * 360;
        var saturation = 1 - Math.Clamp(point.Y / bounds.Height, 0, 1);
        SelectedByUser?.Invoke(hue, saturation);
    }

    // Arrow keys move the selection one step (a degree of hue, a percent of saturation), ten with
    // Command or Control, as WinUI's do.
    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        foreach (var press in presses.ToArray<UIPress>())
        {
            if (press.Key is not { } key)
            {
                continue;
            }

            var step = (key.ModifierFlags & (UIKeyModifierFlags.Command | UIKeyModifierFlags.Control)) != 0 ? 10 : 1;
            var (hueStep, saturationStep) = key.KeyCode switch
            {
                UIKeyboardHidUsage.KeyboardLeftArrow => (-step, 0),
                UIKeyboardHidUsage.KeyboardRightArrow => (step, 0),
                UIKeyboardHidUsage.KeyboardUpArrow => (0, step),
                UIKeyboardHidUsage.KeyboardDownArrow => (0, -step),
                _ => (0, 0),
            };
            if (hueStep != 0 || saturationStep != 0)
            {
                _focusRing.Hidden = false;
                SelectedByUser?.Invoke(Math.Clamp(_hue + hueStep, 0, 360), Math.Clamp(_saturation + (saturationStep / 100d), 0, 1));
                return;
            }
        }

        base.PressesBegan(presses, evt);
    }

    // Hue across, saturation down from 1 to 0, at full value, in sRGB.
    private static CGImage CreateField()
    {
        var pixels = new byte[ImageSize * ImageSize * 4];
        for (var row = 0; row < ImageSize; row++)
        {
            var saturation = 1 - (row / (double)(ImageSize - 1));
            for (var column = 0; column < ImageSize; column++)
            {
                ColorPicker.HsvToRgb(column / (double)(ImageSize - 1) * 360, saturation, 1, out var red, out var green, out var blue);
                var offset = ((row * ImageSize) + column) * 4;
                pixels[offset] = (byte)red;
                pixels[offset + 1] = (byte)green;
                pixels[offset + 2] = (byte)blue;
                pixels[offset + 3] = 255;
            }
        }

        using var colorSpace = CGColorSpace.CreateSrgb();
        using var context = new CGBitmapContext(pixels, ImageSize, ImageSize, 8, ImageSize * 4, colorSpace, CGImageAlphaInfo.NoneSkipLast);
        return context.ToImage()!;
    }
}

/// <summary>
/// The value slider of WinUI's ColorPicker: a 12 pt rounded track from black to the chosen hue and
/// saturation at full value, with WinUI's round thumb (a disc with an accent dot).
/// </summary>
public sealed class ColorPickerSliderView : UIView
{
    private const float TrackHeight = 12;
    private const float ThumbSize = 18;
    private const float ThumbDotSize = 10;

    private readonly CAGradientLayer _track = new();
    private readonly CALayer _thumb = new();
    private readonly CALayer _thumbDot = new();

    private double _hue;
    private double _saturation;
    private double _value = 1;

    public ColorPickerSliderView()
    {
        _track.StartPoint = new CGPoint(0, 0.5);
        _track.EndPoint = new CGPoint(1, 0.5);
        _track.CornerRadius = TrackHeight / 2;
        _track.BorderWidth = 1;
        Layer.AddSublayer(_track);

        _thumb.CornerRadius = ThumbSize / 2;
        _thumb.BorderWidth = 1;
        _thumb.ShadowColor = UIColor.Black.CGColor;
        _thumb.ShadowOpacity = 0.15f;
        _thumb.ShadowRadius = 2;
        _thumb.ShadowOffset = new CGSize(0, 1);
        _thumbDot.CornerRadius = ThumbDotSize / 2;
        _thumb.AddSublayer(_thumbDot);
        Layer.AddSublayer(_thumb);

        IsAccessibilityElement = true;
        AccessibilityTraits = UIAccessibilityTrait.Adjustable;
        AccessibilityLabel = "Value";
        UpdateColors();
    }

    /// <summary>The user set the value (0 to 1) with the pointer or the arrow keys.</summary>
    public Action<double>? ValueSetByUser { get; set; }

    public void Show(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = saturation;
        _value = value;
        UpdateLayers();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        UpdateLayers();
    }

    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        UpdateColors();
    }

    public override void TintColorDidChange()
    {
        base.TintColorDidChange();
        UpdateColors();
    }

    // WinUI's thumb: a disc of the control fill colour (white, dark grey in the dark theme) with a
    // hairline border, and the accent colour in its middle.
    private void UpdateColors()
    {
        var isDark = TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
        _thumb.BackgroundColor = (isDark ? UIColor.FromRGB(0x45, 0x45, 0x45) : UIColor.White).CGColor;
        _thumb.BorderColor = UIColor.Label.ColorWithAlpha(0.15f).CGColor;
        _thumbDot.BackgroundColor = TintColor.CGColor;
        _track.BorderColor = UIColor.Label.ColorWithAlpha(0.2f).CGColor;
    }

    private void UpdateLayers()
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        CATransaction.Begin();
        CATransaction.DisableActions = true;
        _track.Frame = new CGRect(0, (bounds.Height - TrackHeight) / 2, bounds.Width, TrackHeight);
        ColorPicker.HsvToRgb(_hue, _saturation, 1, out var red, out var green, out var blue);
        _track.Colors = [UIColor.Black.CGColor, UIColor.FromRGB(red, green, blue).CGColor];

        // The thumb stays inside the track's ends, as WinUI's does.
        var travel = Math.Max(0, bounds.Width - ThumbSize);
        _thumb.Frame = new CGRect(_value * travel, (bounds.Height - ThumbSize) / 2, ThumbSize, ThumbSize);
        _thumbDot.Frame = new CGRect((ThumbSize - ThumbDotSize) / 2, (ThumbSize - ThumbDotSize) / 2, ThumbDotSize, ThumbDotSize);
        CATransaction.Commit();

        AccessibilityValue = $"{Math.Round(_value * 100)}%";
    }

    public override bool CanBecomeFirstResponder => true;

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        BecomeFirstResponder();
        SetAt(touches);
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt) => SetAt(touches);

    public override void TouchesEnded(NSSet touches, UIEvent? evt) => SetAt(touches);

    private void SetAt(NSSet touches)
    {
        if (touches.AnyObject is UITouch touch)
        {
            SetAt(touch.LocationInView(this));
        }
    }

    /// <summary>Sets the value from a point on the track (clamped to it), as a click there does.</summary>
    internal void SetAt(CGPoint point)
    {
        var travel = Bounds.Width - ThumbSize;
        if (travel > 0)
        {
            ValueSetByUser?.Invoke(Math.Clamp((point.X - (ThumbSize / 2)) / travel, 0, 1));
        }
    }

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        foreach (var press in presses.ToArray<UIPress>())
        {
            if (press.Key is not { } key)
            {
                continue;
            }

            var step = (key.ModifierFlags & (UIKeyModifierFlags.Command | UIKeyModifierFlags.Control)) != 0 ? 0.1 : 0.01;
            var change = key.KeyCode switch
            {
                UIKeyboardHidUsage.KeyboardLeftArrow or UIKeyboardHidUsage.KeyboardDownArrow => -step,
                UIKeyboardHidUsage.KeyboardRightArrow or UIKeyboardHidUsage.KeyboardUpArrow => step,
                _ => 0,
            };
            if (change != 0)
            {
                ValueSetByUser?.Invoke(Math.Clamp(_value + change, 0, 1));
                return;
            }
        }

        base.PressesBegan(presses, evt);
    }
}
