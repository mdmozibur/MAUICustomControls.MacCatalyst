using System.Diagnostics.CodeAnalysis;
using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;
using Microsoft.Maui.Handlers;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Maps <see cref="ComboBox"/> onto a UIKit field plus a drop-down list presented under it.
/// </summary>
/// <remarks>
/// Rows and the field's selection are MAUI views - the item template, or a Label for text rows -
/// converted with <c>ToPlatform</c>, so templates stay in XAML and text rows pick up the app's own
/// Label style. The list is not a <c>UIButton.Menu</c>: on Mac Catalyst that becomes an AppKit
/// pop-up menu, which cannot show a template and takes the title in the accent colour.
/// </remarks>
public sealed class ComboBoxHandler : ViewHandler<ComboBox, ComboBoxView>
{
    public static readonly PropertyMapper<ComboBox, ComboBoxHandler> Mapper =
        new(ViewMapper)
        {
            ["UseSystemFocusVisuals"] = (handler, view) => handler.PlatformView.Field.SetNeedsLayout(),
            [nameof(IView.IsEnabled)] = MapIsEnabled,
            [nameof(IView.AutomationId)] = MapAutomationId,
            [nameof(ComboBox.ItemTemplate)] = MapRows,
            [nameof(ComboBox.DisplayMemberPath)] = MapRows,
            [nameof(ComboBox.FontSize)] = MapRows,
            [nameof(ComboBox.SelectedItemTemplate)] = MapField,
            [nameof(ComboBox.SelectedItem)] = MapField,
            [nameof(ComboBox.Placeholder)] = MapField,
            [nameof(ComboBox.Header)] = MapHeader,
            [nameof(ComboBox.BorderThickness)] = MapBorderThickness,
            [nameof(ComboBox.Padding)] = MapPadding,
            [nameof(ComboBox.CornerRadius)] = MapCornerRadius,
            [nameof(ComboBox.IsDropDownOpen)] = MapIsDropDownOpen
        };

    /// <summary>Gap between the field and the drop-down.</summary>
    private const double DropdownGap = 4;

    /// <summary>How wide the drop-down may grow past a narrow field to fit its rows.</summary>
    private const double DropdownMaxWidth = 420;

    /// <summary>Space kept between a widened drop-down and the window's edge.</summary>
    private const double WindowMargin = 16;

    private ComboBoxDropdownViewController? _dropdown;
    private DropdownTransitioningDelegate? _transitioningDelegate;

    /// <summary>What the field currently shows; the mappers all run on connect, and most change nothing.</summary>
    private FieldState? _fieldState;

    /// <summary>Set while a row click is being applied, so the list it came from is not rebuilt under it.</summary>
    private bool _selecting;

    public ComboBoxHandler() : base(Mapper) { }

    protected override ComboBoxView CreatePlatformView() => new();

    protected override void ConnectHandler(ComboBoxView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.Field.FocusVisualsOwner = VirtualView;
        platformView.Field.TouchUpInside += OnFieldTapped;
        VirtualView.ItemsChanged += OnItemsChanged;
    }

    protected override void DisconnectHandler(ComboBoxView platformView)
    {
        platformView.Field.TouchUpInside -= OnFieldTapped;
        VirtualView.ItemsChanged -= OnItemsChanged;
        CloseDropdown(animated: false);
        _fieldState = null;

        base.DisconnectHandler(platformView);
    }

    private void OnFieldTapped(object? sender, EventArgs e) =>
        VirtualView.IsDropDownOpen = !VirtualView.IsDropDownOpen;

    // The field follows the selection, which the control settles before raising this; only a list
    // that is on screen has to be rebuilt.
    private void OnItemsChanged(object? sender, EventArgs e) => RebuildOpenDropdown();

    private static void MapField(ComboBoxHandler handler, ComboBox view) => handler.UpdateField();

    private static void MapRows(ComboBoxHandler handler, ComboBox view)
    {
        handler.UpdateField();
        handler.RebuildOpenDropdown();
    }

    private static void MapHeader(ComboBoxHandler handler, ComboBox view)
    {
        handler.PlatformView.SetHeader(handler.CreateHeader(view));
        handler.UpdateAccessibility();
        ((IView)view).InvalidateMeasure();
    }

    private static void MapIsEnabled(ComboBoxHandler handler, ComboBox view)
    {
        ViewHandler.MapIsEnabled(handler, view);
        handler.PlatformView.Field.Enabled = view.IsEnabled;
    }

    // MAUI names the platform view, which here is only the container; the field is the element
    // assistive technologies and UI tests act on.
    private static void MapAutomationId(ComboBoxHandler handler, ComboBox view)
    {
        ViewHandler.MapAutomationId(handler, view);
        handler.PlatformView.Field.AccessibilityIdentifier = view.AutomationId;
    }

    private static void MapBorderThickness(ComboBoxHandler handler, ComboBox view) =>
        handler.PlatformView.Field.BorderThickness = view.BorderThickness;

    private static void MapCornerRadius(ComboBoxHandler handler, ComboBox view) =>
        handler.PlatformView.Field.CornerRadius = view.CornerRadius;

    private static void MapPadding(ComboBoxHandler handler, ComboBox view) =>
        handler.PlatformView.Field.ContentInsets = new UIEdgeInsets(
            (nfloat)view.Padding.Top,
            (nfloat)view.Padding.Left,
            (nfloat)view.Padding.Bottom,
            (nfloat)view.Padding.Right);

    private static void MapIsDropDownOpen(ComboBoxHandler handler, ComboBox view)
    {
        if (view.IsDropDownOpen)
            handler.OpenDropdown();
        else
            handler.CloseDropdown(animated: true);
    }

    private void UpdateField()
    {
        var view = VirtualView;
        var state = new FieldState(
            view.SelectedItem, view.EffectiveSelectedItemTemplate, view.DisplayMemberPath, view.Placeholder, view.FontSize);

        if (state == _fieldState)
            return;

        _fieldState = state;
        PlatformView.Field.SetContent(CreateFieldContent(view));
        UpdateAccessibility();
    }

    /// <summary>Builds the collapsed field's content: the selection, or a placeholder label.</summary>
    private UIView CreateFieldContent(ComboBox view)
    {
        if (view.SelectedItem is not { } item)
        {
            return new UILabel
            {
                Text = view.Placeholder,
                TextColor = UIColor.SecondaryLabel,
                Font = UIFont.SystemFontOfSize((nfloat)view.FontSize)!
            };
        }

        return CreateItemView(view, view.EffectiveSelectedItemTemplate, item);
    }

    /// <summary>The view for one item, in a row or in the field: its template, or its text.</summary>
    private UIView CreateItemView(ComboBox view, DataTemplate? template, object item)
    {
        if (template?.CreateContent() is View templated)
        {
            templated.BindingContext = item;
            return Host(templated);
        }

        if (item is SelectorOption option)
            return CreateOptionLabel(option, view.FontSize);

        return Host(CreateTextLabel(view, item));
    }

    /// <summary>
    /// A text row. A MAUI Label rather than a UILabel, so it takes the app's implicit Label style
    /// (font, theme colours) like the labels a template would declare.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DisplayMemberPath names a member of the app's own item types, which the app keeps.")]
    private static View CreateTextLabel(ComboBox view, object item)
    {
        var label = new global::Microsoft.Maui.Controls.Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        if (view.IsSet(ComboBox.FontSizeProperty))
            label.FontSize = view.FontSize;

        if (string.IsNullOrWhiteSpace(view.DisplayMemberPath))
        {
            label.Text = ComboBox.ItemText(item);
        }
        else
        {
            label.BindingContext = item;
            label.SetBinding(global::Microsoft.Maui.Controls.Label.TextProperty,
                new global::Microsoft.Maui.Controls.Binding(view.DisplayMemberPath));
        }

        return label;
    }

    /// <summary>A <see cref="SelectorOption"/> row: its SF Symbol, when it names one, then its text.</summary>
    private static UILabel CreateOptionLabel(SelectorOption option, double fontSize)
    {
        var font = UIFont.SystemFontOfSize((nfloat)fontSize)!;
        var text = new NSMutableAttributedString();

        if (!string.IsNullOrWhiteSpace(option.SystemIconName) &&
            UIImage.GetSystemImage(option.SystemIconName, UIImageSymbolConfiguration.Create(font)) is { } icon)
        {
            text.Append(NSAttributedString.FromAttachment(NSTextAttachment.Create(icon)));
            text.Append(new NSAttributedString("  "));
        }

        text.Append(new NSAttributedString(option.Text));

        return new UILabel
        {
            Font = font,
            TextColor = UIColor.Label,
            AttributedText = text
        };
    }

    private UIView? CreateHeader(ComboBox view) => view.Header switch
    {
        null => null,
        View headerView => Host(headerView),
        var header when header.ToString() is { Length: > 0 } text =>
            Host(new global::Microsoft.Maui.Controls.Label { Text = text }),
        _ => null,
    };

    /// <summary>Hosts a MAUI view inside a UIKit view.</summary>
    private UIView Host(View mauiView)
    {
        if (MauiContext is null)
            return new UIView();

        // Parenting to the control keeps implicit styles, DynamicResource and RelativeSource
        // bindings working.
        mauiView.Parent = VirtualView;

        return new MauiViewHost(mauiView, MauiContext);
    }

    private void UpdateAccessibility()
    {
        var view = VirtualView;
        var field = PlatformView.Field;

        field.AccessibilityLabel = view.Header as string is { Length: > 0 } header ? header : view.Placeholder;

        // A templated item's ToString() is rarely what its row shows, so only text rows report a value.
        field.AccessibilityValue = view.SelectedItem is { } item && view.EffectiveSelectedItemTemplate is null &&
                                   string.IsNullOrWhiteSpace(view.DisplayMemberPath)
            ? ComboBox.ItemText(item)
            : null;
    }

    private void RebuildOpenDropdown()
    {
        if (_dropdown is null || _selecting)
            return;

        CloseDropdown(animated: false, notify: false);
        PresentDropdown(animated: false);
    }

    private void OpenDropdown()
    {
        if (_dropdown is not null || MauiContext is null)
            return;

        // Raised before anything is built, so a handler can still populate the items.
        VirtualView.RaiseDropDownOpened();

        // The handler may have closed the box again.
        if (!VirtualView.IsDropDownOpen || _dropdown is not null)
            return;

        // A box that cannot show a list is put back, or it would be left claiming to be open with
        // nothing on screen.
        if (!PresentDropdown(animated: true))
            VirtualView.IsDropDownOpen = false;
    }

    private bool PresentDropdown(bool animated)
    {
        var view = VirtualView;
        var items = view.ItemList.ToList();
        var presenter = FindPresentingViewController();

        if (items.Count == 0 || presenter is null)
            return false;

        var field = PlatformView.Field;
        var width = (double)field.Bounds.Width;
        var windowWidth = field.Window is { } window ? (double)window.Bounds.Width - (WindowMargin * 2) : DropdownMaxWidth;

        _dropdown = new ComboBoxDropdownViewController(
            items,
            item => CreateItemView(view, view.ItemTemplate, item),
            selectedIndex: view.SelectedIndex,
            width: width,
            maxWidth: Math.Min(DropdownMaxWidth, windowWidth),
            maxHeight: view.MaxDropDownHeight,
            onSelect: Select,
            // Escape closes through the control, so its open state never outlives the list.
            onCancel: () => VirtualView.IsDropDownOpen = false);

        // Loading the view early settles PreferredContentSize, which the presentation
        // controller reads when it computes the frame.
        _ = _dropdown.View;

        _dropdown.ModalPresentationStyle = UIModalPresentationStyle.Custom;

        // Held in a field: TransitioningDelegate is a weak reference on the ObjC side.
        _transitioningDelegate = new DropdownTransitioningDelegate(
            field,
            DropdownGap,
            onOutsideTap: () => VirtualView.IsDropDownOpen = false);

        _dropdown.TransitioningDelegate = _transitioningDelegate;

        field.IsOpen = true;
        presenter.PresentViewController(_dropdown, animated, completionHandler: null);
        return true;
    }

    /// <summary>
    /// A row was chosen. The selection is made while the box still reports itself open, so a
    /// SelectionChanged handler can tell the user's choice from one made in code, as it can in UWP.
    /// </summary>
    private void Select(int index)
    {
        var view = VirtualView;

        _selecting = true;
        try
        {
            view.SelectedIndex = index;
        }
        finally
        {
            _selecting = false;
        }

        view.IsDropDownOpen = false;
    }

    private void CloseDropdown(bool animated, bool notify = true)
    {
        PlatformView.Field.IsOpen = false;

        if (_dropdown is null)
            return;

        var closing = _dropdown;
        _dropdown = null;
        _transitioningDelegate = null;

        var view = VirtualView;
        closing.DismissViewController(animated, notify ? () => view?.RaiseDropDownClosed() : null);
    }

    private UIViewController? FindPresentingViewController()
    {
        var window = PlatformView.Window ?? UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(candidate => candidate.IsKeyWindow);

        var controller = window?.RootViewController;

        while (controller?.PresentedViewController is not null)
            controller = controller.PresentedViewController;

        return controller;
    }

    private sealed record FieldState(
        object? Item, DataTemplate? Template, string? DisplayMemberPath, string Placeholder, double FontSize);
}
