namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using ColorWell = MAUICustomControls.MacCatalyst.Controls.ColorWell;
using ColorPicker = MAUICustomControls.MacCatalyst.Controls.ColorPicker;
using MauiColor = Microsoft.Maui.Graphics.Color;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;

/// <summary>A color swatch with the same embedded picker the app's color chooser uses.</summary>
public sealed class ColorWellHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ColorWell, Button>
{
    public static readonly PropertyMapper<ColorWell, ColorWellHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ColorWell.SelectedColor)] = MapColor,
        [nameof(ColorWell.Title)] = MapTitle,
    };

    private Flyout? _flyout;
    private ColorPicker? _picker;
    private Microsoft.Maui.Controls.Slider? _alpha;
    private bool _updating;

    public ColorWellHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper) { }

    protected override Button CreatePlatformView() => new() { MinWidth = 32, MinHeight = 24 };

    protected override void ConnectHandler(Button platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Click += OnClick;
    }

    protected override void DisconnectHandler(Button platformView)
    {
        platformView.Click -= OnClick;
        _flyout?.Hide();
        _flyout = null;
        _picker = null;
        _alpha = null;
        base.DisconnectHandler(platformView);
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (VirtualView is not { } well || MauiContext is null) return;
        _flyout?.Hide();
        var picker = _picker = new ColorPicker { Color = well.SelectedColor };
        var content = new Microsoft.Maui.Controls.VerticalStackLayout { Spacing = 8, Children = { picker } };
        var alpha = _alpha = new Microsoft.Maui.Controls.Slider
        {
            Minimum = 0, Maximum = 1, Value = well.SelectedColor.Alpha, IsVisible = well.SupportsAlpha,
        };
        Microsoft.Maui.Controls.SemanticProperties.SetDescription(alpha, "Opacity");
        content.Children.Add(alpha);
        void Changed()
        {
            if (!_updating)
            {
                well.SelectedColor = new MauiColor(picker.Color.Red, picker.Color.Green, picker.Color.Blue,
                    well.SupportsAlpha ? (float)alpha.Value : 1f);
            }
        }
        picker.ColorChanged += (_, _) => Changed();
        alpha.ValueChanged += (_, _) => Changed();
        _flyout = new Flyout { Content = LinuxControlSupport.Host(content, well, MauiContext), Placement = PlacementMode.Bottom };
        _flyout.ShowAt(PlatformView);
    }

    private static void MapColor(ColorWellHandler handler, ColorWell well)
    {
        handler.PlatformView.Background = new SolidColorBrush(well.SelectedColor.ToAvalonia());
        handler._updating = true;
        try
        {
            if (handler._picker is { } picker) picker.Color = well.SelectedColor;
            if (handler._alpha is { } alpha) alpha.Value = well.SelectedColor.Alpha;
        }
        finally { handler._updating = false; }
    }

    private static void MapTitle(ColorWellHandler handler, ColorWell well)
    {
        Avalonia.Automation.AutomationProperties.SetName(handler.PlatformView,
            string.IsNullOrWhiteSpace(well.Title) ? "Choose color" : well.Title);
        ToolTip.SetTip(handler.PlatformView, well.Title);
    }
}
