namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Maui.Platform;
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.Maui;
using BaseHandler = Avalonia.Controls.Maui.Handlers.LayoutHandler;
using Thickness = Avalonia.Thickness;
using CornerRadius = Avalonia.CornerRadius;
using Point = Avalonia.Point;

public sealed class BorderedLayoutHandler : Avalonia.Controls.Maui.Handlers.LayoutHandler
{
    public static readonly PropertyMapper<ILayout, BaseHandler> ChromeMapper = new(BaseHandler.Mapper)
    {
        [nameof(IView.Background)] = (handler, layout) =>
        {
            BaseHandler.MapBackground(handler, layout);
            var panel = (BorderedLayoutPanel)handler.PlatformView;
            panel.UpdateBackground(layout.Background is null &&
                (layout as Microsoft.Maui.Controls.VisualElement)?.BackgroundColor is null ? null : panel.Background);
        },
    };
    public BorderedLayoutHandler() : base(ChromeMapper) { }
    // LayoutHandler.SetVirtualView installs the cross-platform measure/arrange
    // delegates on this LayoutPanel, and retains its native child management.
    protected override Panel CreatePlatformView() => new BorderedLayoutPanel { Owner = VirtualView };
    protected override void DisconnectHandler(Panel native)
    {
        ((BorderedLayoutPanel)native).Owner = null;
        base.DisconnectHandler(native);
    }
}

public sealed class BorderedLayoutPanel : LayoutPanel, Avalonia.Rendering.ICustomHitTest
{
    private IBrush? _background;
    private IBrush? _borderBrush;
    private Thickness _borderThickness;
    private CornerRadius _radius;
    internal ILayout? Owner { get; set; }
    public BorderedLayoutPanel()
    {
        PropertyChanged += (_, args) =>
        {
            if (args.Property == BoundsProperty) RefreshChrome();
        };
    }
    internal void UpdateBackground(IBrush? brush)
    {
        _background = brush;
        RefreshChrome();
    }
    internal void UpdateChrome(Microsoft.Maui.Controls.Brush? brush, Microsoft.Maui.Thickness thickness, Microsoft.Maui.CornerRadius radius)
    {
        _borderBrush = brush.ToAvaloniaBrush();
        _borderThickness = new Thickness(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);
        _radius = new CornerRadius(radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        RefreshChrome();
    }
    private void RefreshChrome()
    {
        // An unattached VisualBrush scene can retain its previous render data.
        // Give each chrome update its own visual snapshot to invalidate that cache.
        var chrome = new Border { Width = Bounds.Width, Height = Bounds.Height, Background = _background,
            BorderBrush = _borderBrush, BorderThickness = _borderThickness, CornerRadius = _radius };
        chrome.Measure(Bounds.Size);
        chrome.Arrange(new Rect(Bounds.Size));
        Background = new VisualBrush(chrome) { Stretch = Stretch.Fill };
        InvalidateVisual();
    }
    public bool HitTest(Point point)
    {
        // The compositor's hit-test callback supplies panel-local coordinates.
        // Translating them from the window again rejects painted panels at an offset.
        if (!new Rect(Bounds.Size).Contains(point)) return false;
        if (_background is not null || Owner is Microsoft.Maui.Controls.View { GestureRecognizers.Count: > 0 }) return true;
        // Custom hit testing gates the subtree too, so painted descendants must
        // remain reachable through an otherwise unpainted panel.
        foreach (var child in Children)
            if (child.IsEffectivelyVisible && child.IsHitTestVisible && this.TranslatePoint(point, child) is { } local && child.InputHitTest(local) is not null) return true;
        var border = _borderThickness;
        return _borderBrush is not null &&
            (point.X < border.Left || point.Y < border.Top || point.X >= Bounds.Width - border.Right || point.Y >= Bounds.Height - border.Bottom);
    }
}
