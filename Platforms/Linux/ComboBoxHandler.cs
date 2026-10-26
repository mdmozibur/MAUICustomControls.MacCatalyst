namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;
using AvaloniaComboBox = Avalonia.Controls.ComboBox;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ComboBox = MAUICustomControls.MacCatalyst.Controls.ComboBox;
using MauiDataTemplate = Microsoft.Maui.Controls.DataTemplate;

/// <summary>A header above Avalonia's combo box: UWP's ComboBox.Header, which Avalonia's lacks.</summary>
public sealed class ComboBoxHost : StackPanel
{
    public ComboBoxHost()
    {
        Spacing = 4;
        Children.Add(Box);

        // Without a background only the field's outline and text glyphs are hit-testable, so a
        // click inside the field fell through to whatever was behind it.
        Box.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty && Box.Background is null)
            {
                Box.Background = Brushes.Transparent;
            }
        };
        Box.TemplateApplied += (_, _) => Box.Background ??= Brushes.Transparent;
    }

    public AvaloniaComboBox Box { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };

    public void SetHeader(Control? header)
    {
        if (Children.Count > 1)
        {
            Children.RemoveAt(0);
        }

        if (header is not null)
        {
            Children.Insert(0, header);
        }
    }
}

/// <summary>
/// UWP's ComboBox on Linux: Avalonia's (Fluent) combo box. Rows are the item's text, or its
/// <see cref="ComboBox.ItemTemplate"/> built as MAUI views.
/// </summary>
public sealed class ComboBoxHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ComboBox, ComboBoxHost>
{
    public static readonly PropertyMapper<ComboBox, ComboBoxHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ComboBox.ItemsSource)] = MapItems,
        [nameof(ComboBox.ItemTemplate)] = MapTemplates,
        [nameof(ComboBox.SelectedItemTemplate)] = MapTemplates,
        [nameof(ComboBox.DisplayMemberPath)] = MapTemplates,
        [nameof(ComboBox.FontSize)] = MapFontSize,
        [nameof(ComboBox.SelectedIndex)] = MapSelection,
        [nameof(ComboBox.SelectedItem)] = MapSelection,
        [nameof(ComboBox.Placeholder)] = (handler, view) => handler.PlatformView.Box.PlaceholderText = view.Placeholder,
        [nameof(ComboBox.MaxDropDownHeight)] = MapMaxDropDownHeight,
        [nameof(ComboBox.IsDropDownOpen)] = MapIsDropDownOpen,
        [nameof(ComboBox.BorderThickness)] = MapBorderThickness,
        [nameof(ComboBox.CornerRadius)] = (handler, view) => handler.PlatformView.Box.CornerRadius = new Avalonia.CornerRadius(view.CornerRadius),
        [nameof(ComboBox.Padding)] = MapPadding,
        [nameof(IView.MinimumHeight)] = MapMinimumHeight,
        [nameof(ComboBox.Header)] = MapHeader,
        // The combo box draws its own field; the view's Background paints that, not the header too.
        [nameof(IView.Background)] = (handler, view) => handler.PlatformView.Box.Background = view.Background.ToAvaloniaBrush() ?? handler.PlatformView.Box.Background,
    };

    // Set while the virtual view's items or selection are written to the combo box, so its own
    // SelectionChanged (a reset clears the selection) is not written back.
    private bool _updating;

    public ComboBoxHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override ComboBoxHost CreatePlatformView() => new();

    protected override void ConnectHandler(ComboBoxHost platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Box.SelectionChanged += OnSelectionChanged;
        platformView.Box.DropDownOpened += OnDropDownOpened;
        platformView.Box.DropDownClosed += OnDropDownClosed;
        if (VirtualView is { } view)
        {
            view.ItemsChanged += OnItemsChanged;
            view.ParentChanged += OnParentChanged;
        }
    }

    protected override void DisconnectHandler(ComboBoxHost platformView)
    {
        platformView.Box.SelectionChanged -= OnSelectionChanged;
        platformView.Box.DropDownOpened -= OnDropDownOpened;
        platformView.Box.DropDownClosed -= OnDropDownClosed;
        if (VirtualView is { } view)
        {
            view.ItemsChanged -= OnItemsChanged;
            view.ParentChanged -= OnParentChanged;
        }

        base.DisconnectHandler(platformView);
    }

    // The theme metrics come from the resources of the view's ancestors.
    private void OnParentChanged(object? sender, EventArgs e)
    {
        if (VirtualView is { } view)
        {
            MapFontSize(this, view);
            MapPadding(this, view);
            MapMinimumHeight(this, view);
        }
    }

    /// <summary>
    /// A WinUI theme resource the app overrides for part of its UI (lightweight styling: a pane's
    /// resources set ComboBoxMinHeight, ComboBoxPadding or ControlContentThemeFontSize, and every
    /// combo box in it is compact). The nearest ancestor's value, then the app's; none leaves the
    /// Fluent theme's own.
    /// </summary>
    private static T? ThemeMetric<T>(Microsoft.Maui.Controls.Element view, string key)
        where T : struct
    {
        for (var element = view; element is not null; element = element.Parent)
        {
            if (element is Microsoft.Maui.Controls.VisualElement { Resources: { } resources } && resources.TryGetValue(key, out var value) && value is T metric)
            {
                return metric;
            }
        }

        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var appValue) == true && appValue is T appMetric ? appMetric : null;
    }

    private static double? EffectiveFontSize(ComboBox view) =>
        view.IsSet(ComboBox.FontSizeProperty) ? view.FontSize : ThemeMetric<double>(view, "ControlContentThemeFontSize");

    private void OnItemsChanged(object? sender, EventArgs e)
    {
        if (VirtualView is { } view)
        {
            MapItems(this, view);
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updating && VirtualView is { } view)
        {
            view.SelectedIndex = PlatformView.Box.SelectedIndex;
        }
    }

    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        if (VirtualView is not { } view)
        {
            return;
        }

        view.RaiseDropDownOpened();
        if (!view.IsDropDownOpen)
        {
            view.IsDropDownOpen = true;
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        if (VirtualView is not { } view)
        {
            return;
        }

        if (view.IsDropDownOpen)
        {
            view.IsDropDownOpen = false;
        }

        view.RaiseDropDownClosed();
    }

    // A snapshot rather than the live list: Avalonia clears its selection when its source resets,
    // which would write -1 back over the selection the control has just settled.
    private static void MapItems(ComboBoxHandler handler, ComboBox view)
    {
        handler._updating = true;
        try
        {
            handler.PlatformView.Box.ItemsSource = view.ItemList.ToArray();
            handler.PlatformView.Box.SelectedIndex = view.SelectedIndex;
        }
        finally
        {
            handler._updating = false;
        }
    }

    private static void MapSelection(ComboBoxHandler handler, ComboBox view)
    {
        if (handler.PlatformView.Box.SelectedIndex == view.SelectedIndex)
        {
            return;
        }

        handler._updating = true;
        try
        {
            handler.PlatformView.Box.SelectedIndex = view.SelectedIndex;
        }
        finally
        {
            handler._updating = false;
        }
    }

    private static void MapTemplates(ComboBoxHandler handler, ComboBox view)
    {
        var box = handler.PlatformView.Box;
        box.ItemTemplate = new FuncDataTemplate<object?>((item, _) => handler.CreateItemView(view, view.ItemTemplate, item), supportsRecycling: false);
        box.SelectionBoxItemTemplate = new FuncDataTemplate<object?>((item, _) => handler.CreateItemView(view, view.EffectiveSelectedItemTemplate, item), supportsRecycling: false);
    }

    private static void MapFontSize(ComboBoxHandler handler, ComboBox view)
    {
        if (EffectiveFontSize(view) is { } fontSize)
        {
            handler.PlatformView.Box.FontSize = fontSize;
        }

        MapTemplates(handler, view);
    }

    private static void MapMaxDropDownHeight(ComboBoxHandler handler, ComboBox view)
    {
        if (view.MaxDropDownHeight > 0 && !double.IsInfinity(view.MaxDropDownHeight))
        {
            handler.PlatformView.Box.MaxDropDownHeight = view.MaxDropDownHeight;
        }
    }

    private static void MapIsDropDownOpen(ComboBoxHandler handler, ComboBox view)
    {
        if (handler.PlatformView.Box.IsDropDownOpen != view.IsDropDownOpen)
        {
            handler.PlatformView.Box.IsDropDownOpen = view.IsDropDownOpen;
        }
    }

    private static void MapBorderThickness(ComboBoxHandler handler, ComboBox view)
    {
        if (view.IsSet(ComboBox.BorderThicknessProperty))
        {
            handler.PlatformView.Box.BorderThickness = new Avalonia.Thickness(view.BorderThickness);
        }
    }

    private static void MapPadding(ComboBoxHandler handler, ComboBox view)
    {
        if (view.IsSet(ComboBox.PaddingProperty))
        {
            handler.PlatformView.Box.Padding = view.Padding.ToAvalonia();
        }
        else if (ThemeMetric<Microsoft.Maui.Thickness>(view, "ComboBoxPadding") is { } padding)
        {
            handler.PlatformView.Box.Padding = padding.ToAvalonia();
        }
    }

    private static void MapMinimumHeight(ComboBoxHandler handler, ComboBox view)
    {
        AvaloniaViewHandler.ViewMapper.UpdateProperty(handler, view, nameof(IView.MinimumHeight));
        if (view.IsSet(Microsoft.Maui.Controls.VisualElement.MinimumHeightRequestProperty))
        {
            handler.PlatformView.Box.MinHeight = Math.Max(0, view.MinimumHeightRequest);
        }
        else if (ThemeMetric<double>(view, "ComboBoxMinHeight") is { } minHeight)
        {
            handler.PlatformView.Box.MinHeight = minHeight;
        }
    }

    private static void MapHeader(ComboBoxHandler handler, ComboBox view)
    {
        Control? header = view.Header switch
        {
            null => null,
            Microsoft.Maui.Controls.View headerView => LinuxControlSupport.Host(headerView, view, handler.MauiContext),
            var value when value.ToString() is { Length: > 0 } text => new TextBlock { Text = text },
            _ => null,
        };
        handler.PlatformView.SetHeader(header);
    }

    /// <summary>The view for one item, in a row or in the field: its template, or its text.</summary>
    private Control CreateItemView(ComboBox view, MauiDataTemplate? template, object? item)
    {
        if (item is null)
        {
            return new TextBlock();
        }

        if (template is Microsoft.Maui.Controls.DataTemplateSelector selector)
        {
            template = selector.SelectTemplate(item, view);
        }

        if (template?.CreateContent() is Microsoft.Maui.Controls.View templated)
        {
            templated.BindingContext = item;
            // UWP's item text inherits the combo box's font size; a MAUI Label has its own.
            if (templated is Microsoft.Maui.Controls.Label label && !label.IsSet(Microsoft.Maui.Controls.Label.FontSizeProperty) && EffectiveFontSize(view) is { } fontSize)
            {
                label.FontSize = fontSize;
            }

            if (LinuxControlSupport.Host(templated, view, MauiContext) is { } hosted)
            {
                return hosted;
            }
        }

        if (item is SelectorOption option)
        {
            return LinuxControlSupport.BuildGlyphAndText(
                MauiContext,
                option.IconGlyph,
                option.IconFontFamily,
                option.IconFontSize > 0 ? option.IconFontSize : view.FontSize,
                option.Text,
                view.FontSize,
                Orientation.Horizontal,
                8,
                foreground: null);
        }

        return new TextBlock
        {
            Text = ItemText(view, item),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "DisplayMemberPath names a member of the app's own item types, which the app keeps.")]
    private static string ItemText(ComboBox view, object item)
    {
        if (string.IsNullOrWhiteSpace(view.DisplayMemberPath))
        {
            return ComboBox.ItemText(item);
        }

        object? value = item;
        foreach (var member in view.DisplayMemberPath.Split('.'))
        {
            if (value is null)
            {
                break;
            }

            value = value.GetType().GetProperty(member)?.GetValue(value);
        }

        return value?.ToString() ?? string.Empty;
    }
}
