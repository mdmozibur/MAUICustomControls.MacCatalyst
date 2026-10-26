namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaCheckBox = Avalonia.Controls.CheckBox;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using CheckBox = MAUICustomControls.MacCatalyst.Controls.CheckBox;

/// <summary>UWP's CheckBox on Linux: Avalonia's (Fluent) check box with the caption as its content.</summary>
public sealed class CheckBoxHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<CheckBox, AvaloniaCheckBox>
{
    public static readonly PropertyMapper<CheckBox, CheckBoxHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(CheckBox.IsChecked)] = MapIsChecked,
        [nameof(CheckBox.Text)] = MapCaption,
        [nameof(CheckBox.FontSize)] = MapCaption,
        [nameof(CheckBox.FontAttributes)] = MapCaption,
        [nameof(CheckBox.Foreground)] = MapCaption,
        [nameof(CheckBox.HorizontalContentAlignment)] = MapContentAlignment,
        [nameof(CheckBox.VerticalContentAlignment)] = MapContentAlignment,
    };

    // Set while the virtual view's state is written to the check box, so its change event does not
    // echo it back (and raise Click for a change the user did not make).
    private bool _updating;

    public CheckBoxHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override AvaloniaCheckBox CreatePlatformView()
    {
        var checkBox = new AvaloniaCheckBox { MinWidth = 0, MinHeight = 20, CornerRadius = new Avalonia.CornerRadius(0), Padding = new Avalonia.Thickness(3, 1, 0, 1) };
        checkBox.TemplateApplied += OnTemplateApplied;
        return checkBox;
    }

    /// <summary>
    /// UWP's check box: a square 14 px box at the top of a 20 px row, the caption 3 px after it.
    /// Fluent's is a rounded 20 px box centred in a 32 px row, which made every option list half as
    /// tall again as on Windows. Fluent sets those sizes in its template, which outranks styles.
    /// </summary>
    private static void OnTemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<Border>("NormalRectangle") is not { Parent: Grid boxColumn } box)
        {
            return;
        }

        boxColumn.Height = 17;
        box.Width = box.Height = 14;
        foreach (var viewbox in boxColumn.Children.OfType<Viewbox>())
        {
            viewbox.Width = viewbox.Height = 14;
        }
    }

    protected override void ConnectHandler(AvaloniaCheckBox platformView)
    {
        base.ConnectHandler(platformView);
        platformView.IsCheckedChanged += OnIsCheckedChanged;
        platformView.Click += OnClick;
    }

    protected override void DisconnectHandler(AvaloniaCheckBox platformView)
    {
        platformView.IsCheckedChanged -= OnIsCheckedChanged;
        platformView.Click -= OnClick;
        base.DisconnectHandler(platformView);
    }

    private void OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_updating && VirtualView is { } checkBox)
        {
            checkBox.IsChecked = PlatformView.IsChecked == true;
        }
    }

    // Avalonia raises Click after toggling, so a handler reads the new state, as UWP's does.
    private void OnClick(object? sender, RoutedEventArgs e) => VirtualView?.RaiseClicked();

    private static void MapIsChecked(CheckBoxHandler handler, CheckBox checkBox)
    {
        handler._updating = true;
        try
        {
            handler.PlatformView.IsChecked = checkBox.IsChecked;
        }
        finally
        {
            handler._updating = false;
        }
    }

    private static void MapCaption(CheckBoxHandler handler, CheckBox checkBox)
    {
        if (string.IsNullOrEmpty(checkBox.Text))
        {
            handler.PlatformView.Content = null;
            return;
        }

        var caption = new TextBlock
        {
            Text = checkBox.Text,
            FontSize = checkBox.FontSize,
            FontWeight = checkBox.FontAttributes.HasFlag(Microsoft.Maui.Controls.FontAttributes.Bold) ? FontWeight.SemiBold : FontWeight.Normal,
            FontStyle = checkBox.FontAttributes.HasFlag(Microsoft.Maui.Controls.FontAttributes.Italic) ? FontStyle.Italic : FontStyle.Normal,
            TextWrapping = TextWrapping.Wrap,
        };
        if (checkBox.Foreground.ToAvaloniaBrush() is { } foreground)
        {
            caption.Foreground = foreground;
        }

        handler.PlatformView.Content = caption;
    }

    private static void MapContentAlignment(CheckBoxHandler handler, CheckBox checkBox)
    {
        handler.PlatformView.HorizontalContentAlignment = checkBox.HorizontalContentAlignment.ToHorizontalAlignment();
        handler.PlatformView.VerticalContentAlignment = checkBox.VerticalContentAlignment.ToVerticalAlignment();
    }
}
