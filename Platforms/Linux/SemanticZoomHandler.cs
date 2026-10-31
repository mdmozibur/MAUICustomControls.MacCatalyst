namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SemanticZoom = MAUICustomControls.MacCatalyst.Controls.SemanticZoom;
using NativeContentView = Avalonia.Controls.Maui.Platform.ContentView;

public sealed class SemanticZoomHandler : Avalonia.Controls.Maui.Handlers.ContentViewHandler
{
    protected override NativeContentView CreatePlatformView() => new SemanticZoomView
    {
        CrossPlatformLayout = VirtualView, Owner = (SemanticZoom)VirtualView,
    };
    protected override void DisconnectHandler(NativeContentView native)
    {
        ((SemanticZoomView)native).Owner = null;
        base.DisconnectHandler(native);
    }
}

public sealed class SemanticZoomView : NativeContentView
{
    internal SemanticZoom? Owner { get; set; }
    public SemanticZoomView()
    {
        Focusable = true;
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, (_, args) =>
        {
            if (args.Scale < .9) SetView(false);
            else if (args.Scale > 1.1) SetView(true);
            args.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
    private void SetView(bool zoomedIn)
    {
        if (Owner is { IsEnabled: true } owner) owner.IsZoomedInViewActive = zoomedIn;
    }
    private void OnWheel(object? sender, PointerWheelEventArgs args)
    {
        if ((args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != KeyModifiers.Control || args.Delta.Y == 0) return;
        SetView(args.Delta.Y > 0);
        args.Handled = true;
    }
    // Called before the application's global key pipeline, so a collection's
    // Ctrl+/- changes semantic views rather than zooming the drawing behind it.
    public static bool TryHandleKey(KeyEventArgs args, bool isDown)
    {
        if ((args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != KeyModifiers.Control) return false;
        bool? zoomedIn = args.Key switch { Key.OemMinus or Key.Subtract => false, Key.OemPlus or Key.Add => true, _ => null };
        if (zoomedIn is null || args.Source is not Visual source) return false;
        var view = source.GetVisualAncestors().Prepend(source).OfType<SemanticZoomView>().FirstOrDefault();
        if (view is null || !view.IsEffectivelyEnabled) return false;
        if (isDown) view.SetView(zoomedIn.Value);
        return true;
    }
}
