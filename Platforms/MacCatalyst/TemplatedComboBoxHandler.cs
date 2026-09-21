using MAUICustomControls.MacCatalyst.Controls;
using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Maps <see cref="TemplatedComboBox"/> onto a UIKit field plus a popover list.
/// </summary>
/// <remarks>
/// Item templates stay in XAML: each one is realised as a MAUI view and converted to a
/// platform view with <c>ToPlatform</c>, so nothing that consumes this control has to
/// touch UIKit.
/// </remarks>
public sealed class TemplatedComboBoxHandler : ViewHandler<TemplatedComboBox, ComboBoxFieldView>
{
    public static readonly PropertyMapper<TemplatedComboBox, TemplatedComboBoxHandler> Mapper =
        new(ViewMapper)
        {
            ["UseSystemFocusVisuals"] = (handler, view) => handler.PlatformView.SetNeedsLayout(),
            [nameof(TemplatedComboBox.ItemsSource)] = MapContent,
            [nameof(TemplatedComboBox.ItemTemplate)] = MapContent,
            [nameof(TemplatedComboBox.SelectedItemTemplate)] = MapContent,
            [nameof(TemplatedComboBox.SelectedItem)] = MapContent,
            [nameof(TemplatedComboBox.Placeholder)] = MapContent,
            [nameof(TemplatedComboBox.FontSize)] = MapContent,
            [nameof(TemplatedComboBox.BorderThickness)] = MapBorderThickness,
            [nameof(TemplatedComboBox.Padding)] = MapPadding,
            [nameof(TemplatedComboBox.CornerRadius)] = MapCornerRadius,
            [nameof(TemplatedComboBox.IsDropDownOpen)] = MapIsDropDownOpen
        };

    /// <summary>Gap between the field and the drop-down.</summary>
    private const double DropdownGap = 4;

    private ComboBoxDropdownViewController? _dropdown;
    private DropdownTransitioningDelegate? _transitioningDelegate;

    public TemplatedComboBoxHandler() : base(Mapper) { }

    protected override ComboBoxFieldView CreatePlatformView() => new();

    protected override void ConnectHandler(ComboBoxFieldView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.FocusVisualsOwner = VirtualView;
        platformView.TouchUpInside += OnFieldTapped;
        VirtualView.ItemsChanged += OnItemsChanged;
    }

    protected override void DisconnectHandler(ComboBoxFieldView platformView)
    {
        platformView.TouchUpInside -= OnFieldTapped;
        VirtualView.ItemsChanged -= OnItemsChanged;
        CloseDropdown(animated: false);

        base.DisconnectHandler(platformView);
    }

    private void OnFieldTapped(object? sender, EventArgs e) =>
        VirtualView.IsDropDownOpen = !VirtualView.IsDropDownOpen;

    private void OnItemsChanged(object? sender, EventArgs e) => MapContent(this, VirtualView);

    private static void MapContent(TemplatedComboBoxHandler handler, TemplatedComboBox view)
    {
        handler.PlatformView.SetContent(handler.CreateFieldContent(view));

        // A template or item change while open must rebuild the list.
        if (handler._dropdown is not null)
        {
            handler.CloseDropdown(animated: false);
            handler.OpenDropdown();
        }
    }

    private static void MapBorderThickness(TemplatedComboBoxHandler handler, TemplatedComboBox view) =>
        handler.PlatformView.BorderThickness = view.BorderThickness;

    private static void MapCornerRadius(TemplatedComboBoxHandler handler, TemplatedComboBox view) =>
        handler.PlatformView.CornerRadius = view.CornerRadius;

    private static void MapPadding(TemplatedComboBoxHandler handler, TemplatedComboBox view) =>
        handler.PlatformView.ContentInsets = new UIEdgeInsets(
            (nfloat)view.Padding.Top,
            (nfloat)view.Padding.Left,
            (nfloat)view.Padding.Bottom,
            (nfloat)view.Padding.Right);

    private static void MapIsDropDownOpen(TemplatedComboBoxHandler handler, TemplatedComboBox view)
    {
        if (view.IsDropDownOpen)
            handler.OpenDropdown();
        else
            handler.CloseDropdown(animated: true);
    }

    /// <summary>Builds the collapsed field's content: the templated selection, or a placeholder label.</summary>
    private UIView CreateFieldContent(TemplatedComboBox view)
    {
        if (view.SelectedItem is null || view.EffectiveSelectedItemTemplate is null)
        {
            return new UILabel
            {
                Text = view.SelectedItem?.ToString() ?? view.Placeholder,
                TextColor = view.SelectedItem is null ? UIColor.SecondaryLabel! : UIColor.Label!,
                Font = UIFont.SystemFontOfSize((nfloat)view.FontSize)!
            };
        }

        return CreateTemplatedView(view.EffectiveSelectedItemTemplate, view.SelectedItem);
    }

    /// <summary>Realises a <see cref="DataTemplate"/> for an item and hosts it in a UIKit view.</summary>
    private UIView CreateTemplatedView(DataTemplate template, object item)
    {
        var mauiView = template.CreateContent() as View;

        if (mauiView is null || MauiContext is null)
            return new UIView();

        mauiView.BindingContext = item;

        // Parenting to the control keeps DynamicResource and RelativeSource bindings working.
        mauiView.Parent = VirtualView;

        return new MauiViewHost(mauiView, MauiContext);
    }

    private void OpenDropdown()
    {
        if (_dropdown is not null || MauiContext is null)
            return;

        var view = VirtualView;
        var items = view.ItemList;

        // Raised before anything is built, so a handler can still populate items.
        if (!view.RaiseBeforePopoverOpen())
        {
            view.IsDropDownOpen = false;
            return;
        }

        items = view.ItemList;

        // Any bail-out from here has to put IsDropDownOpen back, or the control is left
        // claiming to be open with nothing on screen.
        if (items.Count == 0 || view.ItemTemplate is null)
        {
            view.IsDropDownOpen = false;
            return;
        }

        var presenter = FindPresentingViewController();
        if (presenter is null)
        {
            view.IsDropDownOpen = false;
            return;
        }

        var width = (double)PlatformView.Bounds.Width;

        _dropdown = new ComboBoxDropdownViewController(
            items,
            item => CreateTemplatedView(view.ItemTemplate, item),
            selectedIndex: view.SelectedItem is null ? -1 : items.IndexOf(view.SelectedItem),
            width: width,
            maxHeight: view.MaxDropDownHeight,
            onSelect: index =>
            {
                view.SelectedItem = items[index];
                view.IsDropDownOpen = false;
            });

        // Loading the view early settles PreferredContentSize, which the presentation
        // controller reads when it computes the frame.
        _ = _dropdown.View;

        _dropdown.ModalPresentationStyle = UIModalPresentationStyle.Custom;

        // Held in a field: TransitioningDelegate is a weak reference on the ObjC side.
        _transitioningDelegate = new DropdownTransitioningDelegate(
            PlatformView,
            DropdownGap,
            onOutsideTap: () => VirtualView.IsDropDownOpen = false);

        _dropdown.TransitioningDelegate = _transitioningDelegate;

        PlatformView.IsOpen = true;
        presenter.PresentViewController(_dropdown, animated: true,
            completionHandler: () => view.RaisePopoverOpened());
    }

    private void CloseDropdown(bool animated)
    {
        PlatformView.IsOpen = false;

        if (_dropdown is null)
            return;

        var closing = _dropdown;
        _dropdown = null;
        _transitioningDelegate = null;

        closing.DismissViewController(animated, () => VirtualView?.RaisePopoverClosed());
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
}
