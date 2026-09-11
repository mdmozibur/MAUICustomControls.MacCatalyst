using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Maps <see cref="ColorWell"/> to UIColorWell, which opens the system color picker on click.
/// </summary>
public sealed class ColorWellHandler : ViewHandler<ColorWell, UIColorWell>
{
    public static readonly PropertyMapper<ColorWell, ColorWellHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ColorWell.SelectedColor)] = MapSelectedColor,
        [nameof(ColorWell.SupportsAlpha)] = MapSupportsAlpha,
        [nameof(ColorWell.Title)] = MapTitle,
    };

    private bool _isUpdatingPlatformView;

    public ColorWellHandler() : base(PropertyMapper)
    {
    }

    protected override UIColorWell CreatePlatformView() => new();

    protected override void ConnectHandler(UIColorWell platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ValueChanged += OnValueChanged;
    }

    protected override void DisconnectHandler(UIColorWell platformView)
    {
        platformView.ValueChanged -= OnValueChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingPlatformView || PlatformView.SelectedColor is not { } selected)
            return;

        // The system picker can hand back colors outside sRGB (e.g. Display P3), whose
        // extended components fall outside 0...1.
        selected.GetRGBA(out var red, out var green, out var blue, out var alpha);
        VirtualView.SelectedColor = new Color(Clamp(red), Clamp(green), Clamp(blue), VirtualView.SupportsAlpha ? Clamp(alpha) : 1f);
    }

    private static float Clamp(nfloat value) => (float)Math.Clamp((double)value, 0d, 1d);

    public static void MapSelectedColor(ColorWellHandler handler, ColorWell colorWell)
    {
        handler._isUpdatingPlatformView = true;
        try
        {
            handler.PlatformView.SelectedColor = colorWell.SelectedColor?.ToPlatform();
        }
        finally
        {
            handler._isUpdatingPlatformView = false;
        }
    }

    public static void MapSupportsAlpha(ColorWellHandler handler, ColorWell colorWell) =>
        handler.PlatformView.SupportsAlpha = colorWell.SupportsAlpha;

    public static void MapTitle(ColorWellHandler handler, ColorWell colorWell) =>
        handler.PlatformView.Title = colorWell.Title;
}
