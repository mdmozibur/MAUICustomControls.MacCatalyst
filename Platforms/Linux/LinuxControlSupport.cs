namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

// Inside the namespace, so Avalonia's names win over MAUI's global usings (Colors, Border, Thickness).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Maui;
using Avalonia.Controls.Maui.Extensions;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using MauiColor = Microsoft.Maui.Graphics.Color;
using MauiThickness = Microsoft.Maui.Thickness;
using MauiView = Microsoft.Maui.Controls.View;

/// <summary>Conversions and lookups the Linux handlers share.</summary>
internal static class LinuxControlSupport
{
    public static Avalonia.Media.Color ToAvalonia(this MauiColor color) => Avalonia.Controls.Maui.Extensions.ColorExtensions.ToAvaloniaColor(color);

    public static IBrush? ToAvaloniaBrush(this Microsoft.Maui.Controls.Brush? brush) => brush switch
    {
        Microsoft.Maui.Controls.SolidColorBrush { Color: { } color } => new SolidColorBrush(color.ToAvalonia()),
        _ => null,
    };

    public static Avalonia.Thickness ToAvalonia(this MauiThickness thickness) =>
        new(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);

    public static HorizontalAlignment ToHorizontalAlignment(this Microsoft.Maui.Controls.LayoutOptions options) => options.Alignment switch
    {
        Microsoft.Maui.Controls.LayoutAlignment.Start => HorizontalAlignment.Left,
        Microsoft.Maui.Controls.LayoutAlignment.End => HorizontalAlignment.Right,
        Microsoft.Maui.Controls.LayoutAlignment.Fill => HorizontalAlignment.Stretch,
        _ => HorizontalAlignment.Center,
    };

    public static VerticalAlignment ToVerticalAlignment(this Microsoft.Maui.Controls.LayoutOptions options) => options.Alignment switch
    {
        Microsoft.Maui.Controls.LayoutAlignment.Start => VerticalAlignment.Top,
        Microsoft.Maui.Controls.LayoutAlignment.End => VerticalAlignment.Bottom,
        Microsoft.Maui.Controls.LayoutAlignment.Fill => VerticalAlignment.Stretch,
        _ => VerticalAlignment.Center,
    };

    /// <summary>
    /// The Avalonia font family for a MAUI family name or alias ("smartcadfont" from ConfigureFonts),
    /// resolved by the backend's font manager so registered fonts load from the app.
    /// </summary>
    public static FontFamily? ResolveFontFamily(Microsoft.Maui.IMauiContext? context, string? family, double size = 14)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        var manager = context?.Services.GetService<Microsoft.Maui.IFontManager>();
        return manager is null ? new FontFamily(family) : manager.GetFontFamily(Microsoft.Maui.Font.OfSize(family, size));
    }

    public static bool IsDark(this Control control) => control.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>The theme's text colour, which Fluent sets on the window and text inherits.</summary>
    public static Avalonia.Media.Color ThemeForeground(Control control) =>
        control.IsDark() ? Colors.White : Avalonia.Media.Color.FromRgb(0x1B, 0x1B, 0x1B);

    /// <summary>The theme's accent (Fluent's SystemAccentColor), DodgerBlue when there is none.</summary>
    public static Avalonia.Media.Color AccentColor(Control control) =>
        control.TryFindResource("SystemAccentColor", control.ActualThemeVariant, out var value) && value is Avalonia.Media.Color color
            ? color
            : Colors.DodgerBlue;

    public static Avalonia.Media.Color WithAlpha(this Avalonia.Media.Color color, double alpha) =>
        Avalonia.Media.Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    /// <summary>
    /// The Avalonia control of a MAUI view built outside the visual tree (a template row, a header, a
    /// popover). Parenting it to the owning control keeps implicit styles, DynamicResource and
    /// RelativeSource bindings working.
    /// </summary>
    public static Control? Host(MauiView view, Microsoft.Maui.Controls.Element owner, Microsoft.Maui.IMauiContext? context)
    {
        if (context is null)
        {
            return null;
        }

        view.Parent ??= owner;
        return view.ToPlatform(context) as Control;
    }

    /// <summary>A glyph from an icon font, then text, side by side or stacked.</summary>
    public static Control BuildGlyphAndText(
        Microsoft.Maui.IMauiContext? context,
        string? glyph,
        string? glyphFontFamily,
        double glyphSize,
        string? text,
        double fontSize,
        Orientation orientation,
        double spacing,
        IBrush? foreground,
        FontWeight weight = FontWeight.Normal)
    {
        var panel = new StackPanel
        {
            Orientation = orientation,
            Spacing = spacing,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (!string.IsNullOrEmpty(glyph))
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontSize = glyphSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (ResolveFontFamily(context, glyphFontFamily, glyphSize) is { } family)
            {
                icon.FontFamily = family;
            }

            if (foreground is not null)
            {
                icon.Foreground = foreground;
            }

            panel.Children.Add(icon);
        }

        if (!string.IsNullOrEmpty(text))
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = orientation == Orientation.Vertical ? TextAlignment.Center : TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (foreground is not null)
            {
                label.Foreground = foreground;
            }

            panel.Children.Add(label);
        }

        return panel;
    }
}

/// <summary>
/// A clickable face drawn by the handlers themselves rather than Fluent's button template, whose
/// checked state fills the button with the accent (UWP's toggle button only tints its content):
/// a border with a background, a translucent wash while the pointer is over it or pressing it, and
/// a click from the pointer, Space or Return.
/// </summary>
public class PressableFace : Border
{
    private bool _pressed;
    private IBrush? _faceBackground;

    public PressableFace()
    {
        Focusable = true;
        ClipToBounds = true;
        Background = Brushes.Transparent;
    }

    public event EventHandler? Click;

    /// <summary>The background at rest; hover and press wash over it.</summary>
    public IBrush? FaceBackground
    {
        get => _faceBackground;
        set
        {
            _faceBackground = value;
            UpdateBackground();
        }
    }

    /// <summary>Keeps the pressed wash on, for a face whose menu is open.</summary>
    public bool IsHeld
    {
        get => field;
        set
        {
            field = value;
            UpdateBackground();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = true;
        e.Pointer.Capture(this);
        e.Handled = true;
        UpdateBackground();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        UpdateBackground();
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        UpdateBackground();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter && !e.Handled)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty)
        {
            UpdateBackground();
        }
    }

    private void UpdateBackground()
    {
        var wash = _pressed || IsHeld ? 0.12 : IsPointerOver ? 0.06 : 0;
        if (wash == 0)
        {
            Background = _faceBackground ?? Brushes.Transparent;
            return;
        }

        // UWP's ButtonBackgroundPointerOver/Pressed: the theme's text colour at low opacity over the
        // resting fill.
        var overlay = LinuxControlSupport.ThemeForeground(this).WithAlpha(this.IsDark() ? wash * 1.4 : wash);
        Background = _faceBackground is ISolidColorBrush { Color: var restColor } && restColor.A > 0
            ? new SolidColorBrush(Blend(restColor, overlay))
            : new SolidColorBrush(overlay);
    }

    private static Avalonia.Media.Color Blend(Avalonia.Media.Color under, Avalonia.Media.Color over)
    {
        var a = over.A / 255.0;
        byte Mix(byte u, byte o) => (byte)Math.Round(u * (1 - a) + o * a);
        return Avalonia.Media.Color.FromArgb(under.A, Mix(under.R, over.R), Mix(under.G, over.G), Mix(under.B, over.B));
    }
}
