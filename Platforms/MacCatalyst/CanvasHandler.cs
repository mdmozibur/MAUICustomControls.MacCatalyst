using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Keeps a <see cref="Canvas"/> out of MAUI's container view.
///
/// MAUI moves a view into a WrapperView as soon as it has a Clip, a Shadow or a border. WrapperView
/// is a plain UIView with no hit-test override, so it answers every hit inside the Canvas bounds and
/// swallows the pointer, pinch and scroll gestures meant for whatever sits behind it — exactly the
/// input an unpainted Canvas is supposed to let through (see <see cref="Canvas"/>). Suppressing the
/// container leaves the LayoutView outermost, and its hit test honours InputTransparent.
///
/// The only thing the container did for a Canvas is clipping, and a Canvas only ever clips to its
/// own bounds, which the platform view does itself through ClipsToBounds.
/// </summary>
public sealed class CanvasHandler : LayoutHandler
{
    public static readonly IPropertyMapper<Canvas, CanvasHandler> Mapper =
        new PropertyMapper<Canvas, CanvasHandler>(LayoutHandler.Mapper)
        {
            [nameof(IView.Clip)] = MapClipToBounds,
        };

    public CanvasHandler()
        : base(Mapper)
    {
    }

    public override bool NeedsContainer => false;

    private static void MapClipToBounds(CanvasHandler handler, Canvas canvas)
    {
        if (handler.PlatformView is { } platformView)
        {
            platformView.ClipsToBounds = canvas.Clip is not null;
        }
    }
}
