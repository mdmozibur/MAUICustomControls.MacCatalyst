namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

// Inside the namespace, so Avalonia's names win over MAUI's global usings.
using Avalonia.Controls;
using MAUICustomControls.MacCatalyst.Controls;
using MauiMenuFlyout = Microsoft.Maui.Controls.MenuFlyout;
using MauiMenuFlyoutItem = Microsoft.Maui.Controls.MenuFlyoutItem;
using MauiMenuFlyoutSeparator = Microsoft.Maui.Controls.MenuFlyoutSeparator;
using MauiMenuFlyoutSubItem = Microsoft.Maui.Controls.MenuFlyoutSubItem;

/// <summary>
/// Shows a MAUI MenuFlyout as an Avalonia menu at an element: a button's menu (UWP Button.Flyout)
/// or a menu the code opens (UWP MenuFlyout.ShowAt). The menu is built when it opens, after
/// <see cref="ContextMenuFlyout.Opening"/> has run, so handlers can still fill or change it.
/// Hidden items (<see cref="IHideableMenuElement.IsVisible"/>) are left out, separators are never
/// doubled or left at an end, <see cref="AppToggleMenuItem"/> shows its check mark and submenus
/// open as submenus.
/// </summary>
public static class MenuPresenter
{
    /// <summary>
    /// Opens <paramref name="menu"/> below <paramref name="target"/>, or with its top-left corner at
    /// <paramref name="position"/> (in <paramref name="target"/>'s coordinates) when one is given.
    /// </summary>
    public static void ShowAt(MauiMenuFlyout menu, Microsoft.Maui.Controls.VisualElement target, Microsoft.Maui.Graphics.Point? position = null)
    {
        if (target.Handler?.PlatformView is not Control anchor)
        {
            return;
        }

        var contextMenu = menu as ContextMenuFlyout;
        contextMenu?.RaiseOpening();

        var flyout = new MenuFlyout();
        foreach (var item in BuildItems(menu))
        {
            flyout.Items.Add(item);
        }

        if (flyout.Items.Count == 0)
        {
            // Every item hidden: UWP shows nothing (and raises no Closed).
            return;
        }

        if (position is { } point)
        {
            flyout.Placement = PlacementMode.AnchorAndGravity;
            flyout.PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.TopLeft;
            flyout.PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.BottomRight;
            flyout.HorizontalOffset = point.X;
            flyout.VerticalOffset = point.Y;
        }
        else
        {
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
        }

        ContextMenuFlyout.OpenContextMenu = menu;
        flyout.Closed += (_, _) =>
        {
            if (ReferenceEquals(ContextMenuFlyout.OpenContextMenu, menu))
            {
                ContextMenuFlyout.OpenContextMenu = null;
            }

            contextMenu?.RaiseClosed();
        };
        flyout.ShowAt(anchor);
    }

    private static List<Control> BuildItems(IEnumerable<Microsoft.Maui.IMenuElement> elements)
    {
        var items = new List<Control>();
        var pendingSeparator = false;
        foreach (var element in elements)
        {
            if (MenuVisibility.IsHidden(element))
            {
                continue;
            }

            if (element is MauiMenuFlyoutSeparator)
            {
                pendingSeparator = items.Count > 0;
                continue;
            }

            var item = element switch
            {
                MauiMenuFlyoutSubItem subItem => CreateSubmenu(subItem),
                MauiMenuFlyoutItem menuItem => CreateItem(menuItem),
                _ => null,
            };
            if (item is null)
            {
                continue;
            }

            if (pendingSeparator)
            {
                items.Add(new Separator());
                pendingSeparator = false;
            }

            items.Add(item);
        }

        return items;
    }

    private static MenuItem? CreateSubmenu(MauiMenuFlyoutSubItem subItem)
    {
        var children = BuildItems(subItem);
        if (children.Count == 0)
        {
            return null;
        }

        var item = new MenuItem { Header = subItem.Text, IsEnabled = subItem.IsEnabled };
        foreach (var child in children)
        {
            item.Items.Add(child);
        }

        return item;
    }

    private static MenuItem CreateItem(MauiMenuFlyoutItem source)
    {
        var item = new MenuItem { Header = source.Text, IsEnabled = source.IsEnabled };
        if (source is AppToggleMenuItem toggle)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = toggle.IsChecked;
        }

        // Activate raises Clicked and runs the Command, as choosing the item does on the other platforms.
        item.Click += (_, _) => ((Microsoft.Maui.Controls.IMenuItemController)source).Activate();
        return item;
    }
}
