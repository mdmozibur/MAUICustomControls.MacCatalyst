namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using MAUICustomControls.MacCatalyst.Controls;
using AvaloniaContentView = Avalonia.Controls.Maui.Platform.ContentView;
using AvaloniaContentViewHandler = Avalonia.Controls.Maui.Handlers.ContentViewHandler;

/// <summary>
/// A button showing its content and opening <see cref="PopoverButton.PopoverContent"/> in an
/// Avalonia flyout anchored to it (UWP's Button.Flyout).
/// </summary>
public sealed class PopoverButtonHandler : AvaloniaContentViewHandler
{
    private Flyout? _flyout;
    private Microsoft.Maui.Controls.View? _flyoutContent;
    private bool _pressed;

    public PopoverButtonHandler() : base(Mapper)
    {
    }

    private PopoverButton? Button => VirtualView as PopoverButton;

    private bool IsFlyoutOpen => _flyout?.IsOpen == true;

    protected override void ConnectHandler(AvaloniaContentView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Focusable = true;
        platformView.Background ??= Brushes.Transparent; // Hit-testable across its whole face.
        platformView.PointerPressed += OnPointerPressed;
        platformView.PointerReleased += OnPointerReleased;
        platformView.KeyDown += OnKeyDown;

        if (Button is { } button)
        {
            button.ShowPopoverAction = () => ShowFlyout(button);
            button.HidePopoverAction = HideFlyout;
        }
    }

    protected override void DisconnectHandler(AvaloniaContentView platformView)
    {
        platformView.PointerPressed -= OnPointerPressed;
        platformView.PointerReleased -= OnPointerReleased;
        platformView.KeyDown -= OnKeyDown;

        if (Button is { } button)
        {
            button.ShowPopoverAction = null;
            button.HidePopoverAction = null;
        }

        HideFlyout();
        ReleaseFlyout();
        base.DisconnectHandler(platformView);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(PlatformView).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        if (new Avalonia.Rect(PlatformView.Bounds.Size).Contains(e.GetPosition(PlatformView)))
        {
            OnTapped();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            OnTapped();
        }
    }

    private void OnTapped()
    {
        if (Button is not { IsEnabled: true } button)
        {
            return;
        }

        button.RaiseClicked();

        if (IsFlyoutOpen)
        {
            HideFlyout();
            return;
        }

        // A flyout the code opens (ShowPopover) is not opened by the click itself.
        if (button.OpensOnClick)
        {
            ShowFlyout(button);
        }
    }

    private void ShowFlyout(PopoverButton button)
    {
        if (button.GetPresentedContent() is not { } content || IsFlyoutOpen || MauiContext is null)
        {
            return;
        }

        button.RaiseOpening();

        var flyout = GetOrCreateFlyout(content);
        if (flyout is null)
        {
            return;
        }

        flyout.Placement = button.PopoverDirection switch
        {
            PopoverDirection.Up => PlacementMode.Top,
            PopoverDirection.Left => PlacementMode.Left,
            PopoverDirection.Right => PlacementMode.Right,
            PopoverDirection.Down => PlacementMode.Bottom,
            _ => PlacementMode.BottomEdgeAlignedLeft,
        };
        if (button.PopoverDirection is PopoverDirection.Up or PopoverDirection.Down)
        {
            flyout.Placement = (button.PopoverDirection, button.PopoverAlignment) switch
            {
                (PopoverDirection.Up, PopoverAlignment.Start) => PlacementMode.TopEdgeAlignedLeft,
                (PopoverDirection.Up, PopoverAlignment.End) => PlacementMode.TopEdgeAlignedRight,
                (PopoverDirection.Down, PopoverAlignment.Start) => PlacementMode.BottomEdgeAlignedLeft,
                (PopoverDirection.Down, PopoverAlignment.End) => PlacementMode.BottomEdgeAlignedRight,
                _ => flyout.Placement,
            };
        }

        flyout.ShowAt(PlatformView);
    }

    private Flyout? GetOrCreateFlyout(Microsoft.Maui.Controls.View content)
    {
        if (_flyout is not null && ReferenceEquals(_flyoutContent, content))
        {
            return _flyout;
        }

        ReleaseFlyout();
        if (Button is not { } button || LinuxControlSupport.Host(content, button, MauiContext) is not { } hosted)
        {
            return null;
        }

        _flyout = new Flyout
        {
            Content = new Border { Padding = button.PopoverPadding.ToAvalonia(), Child = hosted },
            ShowMode = FlyoutShowMode.Standard,
        };
        _flyout.Opened += OnFlyoutOpened;
        _flyout.Closed += OnFlyoutClosed;
        _flyoutContent = content;
        return _flyout;
    }

    private void ReleaseFlyout()
    {
        if (_flyout is not null)
        {
            _flyout.Opened -= OnFlyoutOpened;
            _flyout.Closed -= OnFlyoutClosed;
        }

        _flyoutContent?.Handler?.DisconnectHandler();
        _flyoutContent = null;
        _flyout = null;
    }

    private void HideFlyout() => _flyout?.Hide();

    // Opened is raised once the flyout is on screen, as UWP's Flyout does.
    private void OnFlyoutOpened(object? sender, EventArgs e) => Button?.RaiseOpened();

    private void OnFlyoutClosed(object? sender, EventArgs e) => Button?.RaiseClosed();
}
