namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MAUICustomControls.MacCatalyst.Controls;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ColorPicker = MAUICustomControls.MacCatalyst.Controls.ColorPicker;

/// <summary>The hue/saturation field used by the converted WinUI color picker.</summary>
public sealed class ColorSpectrumHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ColorSpectrum, ColorSpectrumView>
{
    public static readonly PropertyMapper<ColorSpectrum, ColorSpectrumHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ColorSpectrum.Hue)] = MapSelection,
        [nameof(ColorSpectrum.Saturation)] = MapSelection,
        [nameof(ColorSpectrum.Value)] = MapSelection,
        [nameof(ColorSpectrum.CornerRadius)] = MapSelection,
    };

    public ColorSpectrumHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper) { }

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

    private static void MapSelection(ColorSpectrumHandler handler, ColorSpectrum spectrum) =>
        handler.PlatformView.Show(spectrum.Hue, spectrum.Saturation, spectrum.Value, spectrum.CornerRadius);
}

/// <summary>The brightness track used by the converted WinUI color picker.</summary>
public sealed class ColorPickerSliderHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ColorPickerSlider, ColorPickerSliderView>
{
    public static readonly PropertyMapper<ColorPickerSlider, ColorPickerSliderHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ColorPickerSlider.Hue)] = MapColor,
        [nameof(ColorPickerSlider.Saturation)] = MapColor,
        [nameof(ColorPickerSlider.Value)] = MapColor,
    };

    public ColorPickerSliderHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper) { }

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

    private static void MapColor(ColorPickerSliderHandler handler, ColorPickerSlider slider) =>
        handler.PlatformView.Show(slider.Hue, slider.Saturation, slider.Value);
}

/// <summary>Captures a primary-button drag, keeping the selection clamped outside the control.</summary>
public abstract class ColorPickerInputView : Control
{
    protected ColorPickerInputView()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    protected abstract void SelectAt(Point point);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        SelectAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            SelectAt(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (ReferenceEquals(e.Pointer.Captured, this) && e.InitialPressMouseButton == MouseButton.Left)
        {
            SelectAt(e.GetPosition(this));
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }
}

public sealed class ColorSpectrumView : ColorPickerInputView
{
    private static readonly LinearGradientBrush HueBrush = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Colors.Red, 0), new(Colors.Yellow, 1d / 6), new(Colors.Lime, 2d / 6),
            new(Colors.Cyan, 3d / 6), new(Colors.Blue, 4d / 6), new(Colors.Magenta, 5d / 6), new(Colors.Red, 1),
        },
    };

    private static readonly LinearGradientBrush SaturationBrush = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops { new(Color.FromArgb(0, 255, 255, 255), 0), new(Colors.White, 1) },
    };

    private double _hue;
    private double _saturation;
    private double _value = 1;
    private double _cornerRadius = 4;

    public ColorSpectrumView() => AutomationProperties.SetName(this, "Color spectrum");

    public Action<double, double>? SelectedByUser { get; set; }

    public void Show(double hue, double saturation, double value, double cornerRadius)
    {
        _hue = Math.Clamp(hue, 0, 360);
        _saturation = Math.Clamp(saturation, 0, 1);
        _value = Math.Clamp(value, 0, 1);
        _cornerRadius = Math.Max(0, cornerRadius);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var rounded = new RoundedRect(bounds, _cornerRadius);
        using (context.PushClip(rounded))
        {
            context.DrawRectangle(HueBrush, null, bounds);
            context.DrawRectangle(SaturationBrush, null, bounds);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Round((1 - _value) * 255), 0, 0, 0)), null, bounds);
        }

        context.DrawRectangle(null, new Pen(Brushes.Gray, 1), rounded);
        var point = new Point(_hue / 360 * bounds.Width, (1 - _saturation) * bounds.Height);
        ColorPicker.HsvToRgb(_hue, _saturation, _value, out var red, out var green, out var blue);
        var light = (0.299 * red + 0.587 * green + 0.114 * blue) / 255 > 0.5;
        if (IsKeyboardFocusWithin)
        {
            context.DrawEllipse(null, new Pen(light ? Brushes.White : Brushes.Black, 2), point, 9, 9);
        }
        context.DrawEllipse(null, new Pen(light ? Brushes.Black : Brushes.White, 2), point, 7, 7);
    }

    protected override void SelectAt(Point point)
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            SelectedByUser?.Invoke(Math.Clamp(point.X / Bounds.Width, 0, 1) * 360, 1 - Math.Clamp(point.Y / Bounds.Height, 0, 1));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 10 : 1;
        var (hue, saturation) = e.Key switch
        {
            Key.Left => (-step, 0), Key.Right => (step, 0),
            Key.Up => (0, step), Key.Down => (0, -step), _ => (0, 0),
        };
        if (hue != 0 || saturation != 0)
        {
            SelectedByUser?.Invoke(Math.Clamp(_hue + hue, 0, 360), Math.Clamp(_saturation + saturation / 100d, 0, 1));
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}

public sealed class ColorPickerSliderView : ColorPickerInputView
{
    private const double ThumbRadius = 8;
    private double _value = 1;
    private Color _color = Colors.White;

    public ColorPickerSliderView() => AutomationProperties.SetName(this, "Color brightness");

    public Action<double>? ValueSetByUser { get; set; }

    public void Show(double hue, double saturation, double value)
    {
        _value = Math.Clamp(value, 0, 1);
        ColorPicker.HsvToRgb(hue, saturation, 1, out var red, out var green, out var blue);
        _color = Color.FromRgb((byte)red, (byte)green, (byte)blue);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Math.Max(0, Bounds.Width - ThumbRadius * 2);
        var centerY = Bounds.Height / 2;
        var track = new Rect(ThumbRadius, centerY - 6, width, 12);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.Black, 0), new(_color, 1) },
        };
        context.DrawRectangle(brush, new Pen(Brushes.Gray, 1), track, 6, 6);
        var thumb = new Point(ThumbRadius + _value * width, centerY);
        if (IsKeyboardFocusWithin)
        {
            context.DrawEllipse(null, new Pen(Brushes.DodgerBlue, 1), thumb, 10, 10);
        }
        context.DrawEllipse(Brushes.White, new Pen(Brushes.Gray, 1), thumb, ThumbRadius, ThumbRadius);
        context.DrawEllipse(Brushes.DodgerBlue, null, thumb, 4, 4);
    }

    protected override void SelectAt(Point point)
    {
        var width = Bounds.Width - ThumbRadius * 2;
        if (width > 0)
        {
            ValueSetByUser?.Invoke(Math.Clamp((point.X - ThumbRadius) / width, 0, 1));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 0.1 : 0.01;
        double? value = e.Key switch
        {
            Key.Left or Key.Down => _value - step, Key.Right or Key.Up => _value + step,
            Key.Home => 0, Key.End => 1, _ => null,
        };
        if (value.HasValue)
        {
            ValueSetByUser?.Invoke(Math.Clamp(value.Value, 0, 1));
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
