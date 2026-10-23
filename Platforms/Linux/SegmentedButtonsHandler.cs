namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MAUICustomControls.MacCatalyst.Controls;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;

/// <summary>
/// UWP's SegmentedButtons on Linux: a row of equal <see cref="PressableFace"/> segments in a
/// bordered strip, the selected one filled with <see cref="SegmentedButtons.TintColor"/>, and the
/// header above it.
/// </summary>
public sealed class SegmentedButtonsHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<SegmentedButtons, StackPanel>
{
    public static readonly PropertyMapper<SegmentedButtons, SegmentedButtonsHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(SegmentedButtons.Items)] = MapAll,
        [nameof(SegmentedButtons.SelectedIndex)] = MapAll,
        [nameof(SegmentedButtons.Header)] = MapAll,
        [nameof(SegmentedButtons.FontSize)] = MapAll,
        [nameof(SegmentedButtons.TextColor)] = MapAll,
        [nameof(SegmentedButtons.SelectedTextColor)] = MapAll,
        [nameof(SegmentedButtons.TintColor)] = MapAll,
    };

    public SegmentedButtonsHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override StackPanel CreatePlatformView() => new() { Spacing = 4 };

    private static void MapAll(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        var panel = handler.PlatformView;
        panel.Children.Clear();

        if (control.GetHeaderText() is { Length: > 0 } header)
        {
            panel.Children.Add(new TextBlock { Text = header });
        }

        var strip = new Grid();
        var tint = control.TintColor.ToAvalonia();
        for (var index = 0; index < control.Items.Count; index++)
        {
            strip.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var item = control.Items[index];
            var selected = index == control.SelectedIndex;
            IBrush? foreground = selected
                ? new SolidColorBrush(control.SelectedTextColor.ToAvalonia())
                : control.TextColor is { } textColor ? new SolidColorBrush(textColor.ToAvalonia()) : null;

            var segment = new PressableFace
            {
                Padding = new Avalonia.Thickness(10, 4),
                BorderBrush = new SolidColorBrush(tint),
                BorderThickness = new Avalonia.Thickness(index == 0 ? 1 : 0, 1, 1, 1),
                FaceBackground = selected ? new SolidColorBrush(tint) : null,
                Child = SegmentContent(handler, item, control.FontSize, foreground),
            };
            var chosen = index;
            segment.Click += (_, _) => control.SelectedIndex = chosen;
            Grid.SetColumn(segment, index);
            strip.Children.Add(segment);
        }

        panel.Children.Add(strip);
    }

    private static Control SegmentContent(SegmentedButtonsHandler handler, SegmentedButtonItem item, double fontSize, IBrush? foreground)
    {
        if (item.Content is Microsoft.Maui.Controls.View view &&
            handler.VirtualView is { } owner &&
            LinuxControlSupport.Host(view, owner, handler.MauiContext) is { } hosted)
        {
            return hosted;
        }

        var text = item.Content switch
        {
            null => string.Empty,
            string value => value,
            Microsoft.Maui.Controls.Label label => label.Text ?? string.Empty,
            var value => value.ToString() ?? string.Empty,
        };

        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (foreground is not null)
        {
            block.Foreground = foreground;
        }

        return block;
    }
}
