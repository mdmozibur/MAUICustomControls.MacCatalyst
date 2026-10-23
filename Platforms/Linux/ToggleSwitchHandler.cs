namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaToggleSwitch = Avalonia.Controls.ToggleSwitch;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ToggleSwitch = MAUICustomControls.MacCatalyst.Controls.ToggleSwitch;

/// <summary>
/// UWP's ToggleSwitch on Linux: Avalonia's (Fluent) switch, which is UWP's own design. The caption
/// is its header; the On/Off labels are left out, as the app's compact style does.
/// </summary>
public sealed class ToggleSwitchHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ToggleSwitch, AvaloniaToggleSwitch>
{
    public static readonly PropertyMapper<ToggleSwitch, ToggleSwitchHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ToggleSwitch.IsOn)] = MapIsOn,
        [nameof(ToggleSwitch.Text)] = MapText,
        [nameof(ToggleSwitch.FontSize)] = MapText,
        [nameof(ToggleSwitch.Foreground)] = MapText,
        [nameof(ToggleSwitch.Padding)] = (handler, view) => handler.PlatformView.Padding = view.Padding.ToAvalonia(),
        [nameof(ToggleSwitch.OnColor)] = MapOnColor,
    };

    private bool _updating;

    public ToggleSwitchHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override AvaloniaToggleSwitch CreatePlatformView() => new()
    {
        OnContent = null,
        OffContent = null,
        MinWidth = 0,
    };

    protected override void ConnectHandler(AvaloniaToggleSwitch platformView)
    {
        base.ConnectHandler(platformView);
        platformView.IsCheckedChanged += OnIsCheckedChanged;
    }

    protected override void DisconnectHandler(AvaloniaToggleSwitch platformView)
    {
        platformView.IsCheckedChanged -= OnIsCheckedChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_updating && VirtualView is { } toggle)
        {
            toggle.IsOn = PlatformView.IsChecked == true;
        }
    }

    private static void MapIsOn(ToggleSwitchHandler handler, ToggleSwitch toggle)
    {
        handler._updating = true;
        try
        {
            handler.PlatformView.IsChecked = toggle.IsOn;
        }
        finally
        {
            handler._updating = false;
        }
    }

    private static void MapText(ToggleSwitchHandler handler, ToggleSwitch toggle)
    {
        if (string.IsNullOrEmpty(toggle.Text))
        {
            handler.PlatformView.Content = null;
            return;
        }

        var caption = new TextBlock { Text = toggle.Text, FontSize = toggle.FontSize };
        if (toggle.Foreground.ToAvaloniaBrush() is { } foreground)
        {
            caption.Foreground = foreground;
        }

        handler.PlatformView.Content = caption;
    }

    // Fluent's switch reads its "on" fill from these resources; set on the control they override the
    // theme's accent for this switch only.
    private static void MapOnColor(ToggleSwitchHandler handler, ToggleSwitch toggle)
    {
        var resources = handler.PlatformView.Resources;
        string[] keys = ["ToggleSwitchFillOn", "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed"];
        foreach (var key in keys)
        {
            resources.Remove(key);
        }

        if (toggle.OnColor is { } color)
        {
            var brush = new SolidColorBrush(color.ToAvalonia());
            foreach (var key in keys)
            {
                resources[key] = brush;
            }
        }
    }
}
