namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Maui;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Maui;
using ContentButton = MAUICustomControls.MacCatalyst.Controls.ContentButton;
using M = Microsoft.Maui.Controls;
using NativeContentView = Avalonia.Controls.Maui.Platform.ContentView;
using BaseHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using Thickness = Avalonia.Thickness;
using CornerRadius = Avalonia.CornerRadius;

// A native Button supplies input, command availability, focus and automation.
// Its content presenter delegates layout to the MAUI ContentView so arbitrary
// content retains its MAUI coordinates and applies Padding exactly once.
public sealed class ContentButtonHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ContentButton, ContentButtonView>
{
    public static readonly PropertyMapper<ContentButton, ContentButtonHandler> Mapper = new(BaseHandler.ViewMapper)
    {
        [nameof(ContentButton.Content)] = MapContent,
        [nameof(ContentButton.Text)] = MapContent,
        [nameof(ContentButton.Padding)] = MapPadding,
        [nameof(ContentButton.Command)] = (handler, view) => handler.PlatformView.Command = view.Command,
        [nameof(ContentButton.CommandParameter)] = (handler, view) => handler.PlatformView.CommandParameter = view.CommandParameter,
        [nameof(ContentButton.BorderColor)] = MapChrome,
        [nameof(ContentButton.BorderWidth)] = MapChrome,
        [nameof(ContentButton.CornerRadius)] = MapChrome,
        [nameof(IView.Background)] = (handler, view) =>
        {
            if (view.Background.ToAvaloniaBrush() is { } brush) handler.PlatformView.Background = brush;
            else handler.PlatformView.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
        },
    };
    private NativeContentView? _body;
    public ContentButtonHandler() : base(Mapper, BaseHandler.ViewCommandMapper) { }
    protected override ContentButtonView CreatePlatformView()
    {
        _body = new NativeContentView { CrossPlatformLayout = VirtualView };
        return new ContentButtonView
        {
            Owner = VirtualView, Content = _body, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
        };
    }
    protected override void ConnectHandler(ContentButtonView native)
    {
        base.ConnectHandler(native);
        native.Click += OnClick;
    }
    protected override void DisconnectHandler(ContentButtonView native)
    {
        native.Click -= OnClick;
        native.Command = null;
        native.Owner = null;
        if (_body is { } body) { body.Children.Clear(); body.CrossPlatformLayout = null; }
        base.DisconnectHandler(native);
    }
    private void OnClick(object? sender, RoutedEventArgs args) => VirtualView?.RaiseClicked();
    private static void MapContent(ContentButtonHandler handler, ContentButton view)
    {
        if (handler._body is not { } body || handler.MauiContext is null) return;
        body.Children.Clear();
        if (((IContentView)view).PresentedContent is IElement content)
            body.Children.Add((Control)content.ToPlatform(handler.MauiContext));
        body.InvalidateMeasure();
        body.InvalidateArrange();
    }
    private static void MapPadding(ContentButtonHandler handler, ContentButton view)
    {
        handler._body?.InvalidateMeasure();
        handler._body?.InvalidateArrange();
    }
    private static void MapChrome(ContentButtonHandler handler, ContentButton view)
    {
        var native = handler.PlatformView;
        native.BorderThickness = new Thickness(view.BorderWidth);
        native.BorderBrush = view.BorderColor is { } color ? new SolidColorBrush(color.ToAvalonia()) : null;
        var radius = view.CornerRadius;
        native.CornerRadius = new CornerRadius(radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
    }
}

public sealed class ContentButtonView : Button
{
    internal ContentButton? Owner { get; set; }
    private bool _cancelPress;
    private bool _spaceDown;
    private bool _releasing;
    protected override Type StyleKeyOverride => typeof(Button);
    protected override void OnKeyDown(KeyEventArgs args)
    {
        if (args.Key is Key.Space or Key.Enter && (!IsFocused || args.KeyModifiers != KeyModifiers.None)) return;
        if (args.Key == Key.Space) { _spaceDown = true; if (!IsPressed) _cancelPress = false; }
        if (args.Key == Key.Escape && IsPressed) { _cancelPress = true; args.Handled = true; return; }
        base.OnKeyDown(args);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        _cancelPress = false;
        base.OnPointerPressed(args);
    }
    protected override void OnKeyUp(KeyEventArgs args)
    {
        if (args.Key != Key.Space) { base.OnKeyUp(args); return; }
        // The packaged Avalonia version invokes on Space-up even without a
        // matching down. A modified shortcut or lost focus must not click.
        if (!_spaceDown) return;
        _releasing = true;
        try { base.OnKeyUp(args); }
        finally { _releasing = false; _cancelPress = false; _spaceDown = false; }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        _releasing = true;
        try { base.OnPointerReleased(args); }
        finally { _releasing = false; _cancelPress = false; }
    }
    protected override void OnLostFocus(FocusChangedEventArgs args)
    {
        base.OnLostFocus(args);
        _cancelPress = false;
        _spaceDown = false;
    }
    protected override void OnClick()
    {
        if (!_cancelPress || !_releasing) base.OnClick();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPressedProperty || change.Property == IsPointerOverProperty || change.Property == IsEffectivelyEnabledProperty)
            if (Owner is { } button)
                M.VisualStateManager.GoToState(button, !IsEffectivelyEnabled ? "Disabled" : IsPressed ? "Pressed" : IsPointerOver ? "PointerOver" : "Normal");
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new ContentButtonPeer(this);
    private sealed class ContentButtonPeer(ContentButtonView owner) : ButtonAutomationPeer(owner)
    {
        protected override string? GetNameCore()
        {
            if (AutomationProperties.GetName(owner) is { Length: > 0 } explicitName) return explicitName;
            if (owner.Owner is not { } button) return base.GetNameCore();
            if (!string.IsNullOrWhiteSpace(button.Text)) return button.Text;
            if (((IContentView)button).PresentedContent is M.View view)
                return string.Join(" ", view.GetVisualTreeDescendants().Prepend(view).OfType<M.Label>().Distinct()
                    .Where(label => label.IsVisible && label.GetType().Name is not ("FontIcon" or "SymbolIcon"))
                    .Select(label => label.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
            return base.GetNameCore();
        }
    }
}
