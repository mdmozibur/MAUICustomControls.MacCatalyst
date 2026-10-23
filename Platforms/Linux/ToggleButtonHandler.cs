namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ToggleButton = MAUICustomControls.MacCatalyst.Controls.ToggleButton;

/// <summary>
/// UWP's ToggleButton on Linux: a <see cref="PressableFace"/> with the icon glyph and text. As on
/// the Mac, checking it tints the content with <see cref="ToggleButton.Foreground"/> (the accent)
/// instead of filling the button.
/// </summary>
public sealed class ToggleButtonHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ToggleButton, PressableFace>
{
    public static readonly PropertyMapper<ToggleButton, ToggleButtonHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ToggleButton.Text)] = MapContent,
        [nameof(ToggleButton.FontSize)] = MapContent,
        [nameof(ToggleButton.IconGlyph)] = MapContent,
        [nameof(ToggleButton.CustomFontFamily)] = MapContent,
        [nameof(ToggleButton.ImageSpacing)] = MapContent,
        [nameof(ToggleButton.Orientation)] = MapContent,
        [nameof(ToggleButton.Foreground)] = MapContent,
        [nameof(ToggleButton.HorizontalContentAlignment)] = MapContent,
        [nameof(ToggleButton.IsChecked)] = MapIsChecked,
        [nameof(ToggleButton.Padding)] = MapAppearance,
        [nameof(ToggleButton.BorderThickness)] = MapAppearance,
        [nameof(ToggleButton.BorderBrush)] = MapAppearance,
        [nameof(IView.Background)] = MapAppearance,
    };

    public ToggleButtonHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override PressableFace CreatePlatformView() => new();

    protected override void ConnectHandler(PressableFace platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Click += OnClick;
        platformView.ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void DisconnectHandler(PressableFace platformView)
    {
        platformView.Click -= OnClick;
        platformView.ActualThemeVariantChanged -= OnThemeChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnClick(object? sender, EventArgs e)
    {
        if (VirtualView is not { IsEnabled: true } toggle)
        {
            return;
        }

        toggle.IsChecked = !toggle.IsChecked;
        toggle.RaiseClicked();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (VirtualView is { } toggle)
        {
            MapContent(this, toggle);
            MapAppearance(this, toggle);
        }
    }

    private static void MapIsChecked(ToggleButtonHandler handler, ToggleButton toggle)
    {
        MapContent(handler, toggle);
        MapAppearance(handler, toggle);
    }

    private static void MapContent(ToggleButtonHandler handler, ToggleButton toggle)
    {
        var face = handler.PlatformView;
        // Unchecked content follows the theme's text colour; checked content takes the accent.
        IBrush? foreground = toggle.IsChecked
            ? toggle.Foreground.ToAvaloniaBrush() ?? new SolidColorBrush(LinuxControlSupport.AccentColor(face))
            : null;

        var content = LinuxControlSupport.BuildGlyphAndText(
            handler.MauiContext,
            toggle.IconGlyph,
            toggle.CustomFontFamily,
            toggle.FontSize,
            toggle.Text,
            toggle.FontSize,
            toggle.Orientation == Microsoft.Maui.Controls.StackOrientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
            toggle.ImageSpacing,
            foreground);
        content.HorizontalAlignment = toggle.HorizontalContentAlignment.ToHorizontalAlignment();
        face.Child = content;
    }

    private static void MapAppearance(ToggleButtonHandler handler, ToggleButton toggle)
    {
        var face = handler.PlatformView;
        face.Padding = toggle.Padding.ToAvalonia();
        face.BorderThickness = toggle.BorderThickness.ToAvalonia();
        face.FaceBackground = toggle.Background.ToAvaloniaBrush();

        var fallback = toggle.IsChecked
            ? LinuxControlSupport.AccentColor(face).WithAlpha(0.75)
            : LinuxControlSupport.ThemeForeground(face).WithAlpha(0.35);
        face.BorderBrush = toggle.BorderBrush.ToAvaloniaBrush() ?? new SolidColorBrush(fallback);
    }
}
