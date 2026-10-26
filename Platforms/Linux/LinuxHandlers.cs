using Microsoft.Maui.Hosting;

namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

/// <summary>
/// The Linux handlers of the library's controls: Avalonia controls behind the MAUI views (MAUI on
/// Avalonia's MAUI backend), as Platforms/MacCatalyst has UIKit ones. The converter copies this folder
/// into a Linux app (Platforms/Linux) and its MauiProgram calls <see cref="Register"/> after
/// UseAvaloniaApp, so these win over the backend's own handlers.
/// </summary>
public static class LinuxHandlers
{
    public static void Register(IMauiHandlersCollection handlers)
    {
        handlers.AddHandler(typeof(Controls.ToggleButton), typeof(ToggleButtonHandler));
        handlers.AddHandler(typeof(Controls.CheckBox), typeof(CheckBoxHandler));
        handlers.AddHandler(typeof(Controls.ToggleSwitch), typeof(ToggleSwitchHandler));
        handlers.AddHandler(typeof(Controls.ComboBox), typeof(ComboBoxHandler));
        handlers.AddHandler(typeof(Controls.PopoverButton), typeof(PopoverButtonHandler));
        handlers.AddHandler(typeof(Controls.ToggleDropdown), typeof(ToggleDropdownHandler));
        handlers.AddHandler(typeof(Controls.SegmentedButtons), typeof(SegmentedButtonsHandler));
        handlers.AddHandler(typeof(Controls.ProgressRing), typeof(ProgressRingHandler));
        handlers.AddHandler(typeof(Controls.AnimatedVisualPlayer), typeof(AnimatedVisualPlayerHandler));
        handlers.AddHandler(typeof(Controls.AppMenuBarHost), typeof(AppMenuBarHostHandler));

        TreatColorlessBrushesAsEmpty();
        JustifyLabels();
        ListNotifyingItemsSources();
        LetUnpaintedLayoutsPassInput();
        ShowContextMenusThroughPresenter();
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Avalonia.Controls.Control, Microsoft.Maui.Controls.VisualElement> ContextMenuOwners = new();

    /// <summary>
    /// The backend turns FlyoutBase.ContextFlyout into an Avalonia ContextMenu built once, so UWP's
    /// Opening never ran and hidden items stayed in it (the canvas menu showed every command).
    /// A context request opens the MAUI menu through <see cref="MenuPresenter"/> instead, at the
    /// pointer, as the Mac's ContextMenuSupport does.
    /// </summary>
    private static void ShowContextMenusThroughPresenter()
    {
        Avalonia.Controls.Maui.Handlers.ViewHandler.ViewMapper.ModifyMapping<IView, IViewHandler>(
            nameof(IContextFlyoutElement.ContextFlyout),
            (handler, view, _) =>
            {
                if (handler.PlatformView is not Avalonia.Controls.Control control)
                {
                    return;
                }

                control.ContextMenu = null;
                control.RemoveHandler(Avalonia.Controls.Control.ContextRequestedEvent, OnContextRequested);
                ContextMenuOwners.Remove(control);
                if (view is Microsoft.Maui.Controls.VisualElement element and IContextFlyoutElement { ContextFlyout: Microsoft.Maui.Controls.MenuFlyout })
                {
                    ContextMenuOwners.Add(control, element);
                    control.AddHandler(Avalonia.Controls.Control.ContextRequestedEvent, OnContextRequested);
                }
            });
    }

    private static void OnContextRequested(object? sender, Avalonia.Input.ContextRequestedEventArgs e)
    {
        if (e.Handled || sender is not Avalonia.Controls.Control control ||
            !ContextMenuOwners.TryGetValue(control, out var element) ||
            element is not IContextFlyoutElement { ContextFlyout: Microsoft.Maui.Controls.MenuFlyout menu })
        {
            return;
        }

        e.Handled = true;
        Microsoft.Maui.Graphics.Point? position = e.TryGetPosition(control, out var point)
            ? new Microsoft.Maui.Graphics.Point(point.X, point.Y)
            : null;
        MenuPresenter.ShowAt(menu, element, position);
    }

    /// <summary>
    /// The backend paints every layout Transparent, which Avalonia hit-tests, so a layout with no
    /// background of its own swallowed the pointer for whatever lay beneath it (the table overlay
    /// over the drawing canvas took every click and wheel). A layout without a background lets
    /// input through on UWP and MAUI's other platforms; here too, unless it has gesture
    /// recognizers. Elements that listen for the pointer are made hit-testable by the pointer
    /// compat layer when they subscribe.
    /// </summary>
    private static void LetUnpaintedLayoutsPassInput()
    {
        Avalonia.Controls.Maui.Handlers.LayoutHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, layout) =>
        {
            if (layout.Background is null &&
                (layout as Microsoft.Maui.Controls.VisualElement)?.BackgroundColor is null &&
                (layout as Microsoft.Maui.Controls.View)?.GestureRecognizers.Count is null or 0 &&
                handler.PlatformView is Avalonia.Controls.Panel panel &&
                ReferenceEquals(panel.Background, Avalonia.Media.Brushes.Transparent))
            {
                panel.Background = null;
            }
        });
    }

    /// <summary>
    /// Avalonia's items controls take an INotifyCollectionChanged source only when it is also an
    /// IList (they index into it); a grouped or filtered view of the app's that is not threw, and
    /// with it the creation of the pane it was on. Such a source is shown through a list that
    /// follows it.
    /// </summary>
    private static void ListNotifyingItemsSources()
    {
        Avalonia.Controls.Maui.Handlers.CollectionViewHandler.Mapper.ModifyMapping(nameof(ItemsView.ItemsSource), (handler, itemsView, previous) =>
        {
            if (itemsView.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged and not System.Collections.IList &&
                handler.PlatformView is Avalonia.Controls.Maui.MauiCollectionView collectionView)
            {
                collectionView.ItemsSource = new NotifyingListAdapter(itemsView.ItemsSource);
                return;
            }

            previous?.Invoke(handler, itemsView);
        });
    }

    /// <summary>
    /// MAUI's TextAlignment.Justify (UWP's TextAlignment.Justify) is outside the backend's label
    /// mapping, which throws for it; the exception ended the creation of the whole left pane.
    /// Avalonia's TextBlock justifies text itself.
    /// </summary>
    private static void JustifyLabels()
    {
        Avalonia.Controls.Maui.Handlers.LabelHandler.Mapper.ModifyMapping(nameof(ITextAlignment.HorizontalTextAlignment), (handler, label, previous) =>
        {
            if (label.HorizontalTextAlignment == TextAlignment.Justify)
            {
                if (handler.PlatformView is Avalonia.Controls.TextBlock textBlock)
                {
                    textBlock.TextAlignment = Avalonia.Media.TextAlignment.Justify;
                }

                return;
            }

            previous?.Invoke(handler, label);
        });
    }

    /// <summary>
    /// A SolidColorBrush with no Color is no paint on the other MAUI platforms; the backend's shape
    /// mappings dereference the missing colour instead, and the exception ended the handler creation
    /// of the whole page the shape was on (the drawing page never appeared).
    /// </summary>
    private static void TreatColorlessBrushesAsEmpty()
    {
        var mapper = Avalonia.Controls.Maui.Handlers.Shapes.ShapeViewHandler.Mapper;
        mapper.ModifyMapping(nameof(IShapeView.Fill), (handler, view, previous) =>
        {
            if (view.Fill is SolidPaint { Color: null })
            {
                if (handler.PlatformView is Avalonia.Controls.Shapes.Shape shape)
                {
                    shape.Fill = null;
                }

                return;
            }

            previous?.Invoke(handler, view);
        });
        mapper.ModifyMapping(nameof(IStroke.Stroke), (handler, view, previous) =>
        {
            if (view.Stroke is SolidPaint { Color: null })
            {
                if (handler.PlatformView is Avalonia.Controls.Shapes.Shape shape)
                {
                    shape.Stroke = null;
                }

                return;
            }

            previous?.Invoke(handler, view);
        });
    }
}
