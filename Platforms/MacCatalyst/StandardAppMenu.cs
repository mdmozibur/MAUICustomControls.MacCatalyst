using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The menus every macOS app shares, filled from the page's menu bar: items with an
/// <see cref="AppMenuRole"/> move to the application menu (About, Settings…, app items), the Help
/// menu ("&lt;app&gt; Help", Cmd-?) or File > Close (Cmd-W). Call from the app delegate's BuildMenu,
/// after MAUI has added the page's menus.
/// </summary>
public static class StandardAppMenu
{
    /// <summary>The action of the "&lt;app&gt; Help" item; the app delegate exports it and calls <see cref="ShowHelp"/>.</summary>
    public const string ShowHelpSelector = "showAppHelp:";

    // The action MAUI gives its menu bar commands (MauiUIApplicationDelegate.MenuItemSelected).
    private static readonly Selector MenuItemSelector = new("MenuItemSelected:");

    private static string _helpUrl = string.Empty;
    private static WeakReference<AppMenuItem>? _standardHelpItem;

    /// <summary>The app's name as the menu bar shows it (CFBundleName).</summary>
    public static string ApplicationName =>
        NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleName")?.ToString() is { Length: > 0 } name
            ? name
            : AppInfo.Current.Name;

    /// <summary>
    /// Moves the page's role items into the standard menus. <paramref name="helpUrl"/> is what
    /// "&lt;app&gt; Help" opens while no page offers a <see cref="AppMenuRole.StandardHelp"/> item;
    /// <paramref name="settingsItems"/> go under Settings… in the application menu.
    /// </summary>
    public static void Build(IUIMenuBuilder builder, string helpUrl, IReadOnlyList<UIMenuElement> settingsItems)
    {
        _helpUrl = helpUrl;
        var items = CollectRoleItems();
        RemoveFromPageMenus(builder, items);

        UICommand? Command(AppMenuRole role) => items.FirstOrDefault(item => item.Role == role).Command;

        // About: the page's own About item under the macOS title; otherwise AppKit's standard panel.
        if (Command(AppMenuRole.About) is { } about)
        {
            var aboutCommand = Clone(about, $"About {ApplicationName}");
            builder.ReplaceMenu(
                UIMenuIdentifier.About.GetConstant(),
                UIMenu.Create(string.Empty, null, UIMenuIdentifier.About, UIMenuOptions.DisplayInline, [aboutCommand]));
        }

        var preferences = new List<UIMenuElement>();
        if (Command(AppMenuRole.Settings) is { } settings)
        {
            preferences.Add(CloneWithKey(settings, "Settings…", ",", UIKeyModifierFlags.Command));
        }

        preferences.AddRange(settingsItems);
        if (preferences.Count > 0)
        {
            builder.ReplaceMenu(
                UIMenuIdentifier.Preferences.GetConstant(),
                UIMenu.Create(string.Empty, null, UIMenuIdentifier.Preferences, UIMenuOptions.DisplayInline, preferences.ToArray()));
        }

        var applicationItems = items.Where(item => item.Role == AppMenuRole.Application).Select(item => (UIMenuElement)item.Command).ToArray();
        if (applicationItems.Length > 0)
        {
            builder.InsertSiblingMenuAfter(
                UIMenu.Create(string.Empty, null, UIMenuIdentifier.None, UIMenuOptions.DisplayInline, applicationItems),
                UIMenuIdentifier.Preferences.GetConstant());
        }

        BuildHelpMenu(builder, items);
        BuildCloseItem(builder, items.FirstOrDefault(item => item.Role == AppMenuRole.Close));
    }

    /// <summary>"&lt;app&gt; Help": the page's own help item when it offers one, else the help page.</summary>
    public static void ShowHelp()
    {
        if (_standardHelpItem?.TryGetTarget(out var item) == true && IsOnCurrentMenuBar(item))
        {
            ((IMenuElement)item).Clicked();
            return;
        }

        if (Uri.TryCreate(_helpUrl, UriKind.Absolute, out var uri))
        {
            _ = Launcher.Default.OpenAsync(uri);
        }
    }

    private static void BuildHelpMenu(IUIMenuBuilder builder, List<RoleItem> items)
    {
        var standard = items.FirstOrDefault(item => item.Role == AppMenuRole.StandardHelp);
        _standardHelpItem = standard.Item is null ? null : new WeakReference<AppMenuItem>(standard.Item);

        var children = new List<UIMenuElement>();
        if (standard.Item is not null || _helpUrl.Length > 0)
        {
            children.Add(UIKeyCommand.Create($"{ApplicationName} Help", null, new Selector(ShowHelpSelector), "?", UIKeyModifierFlags.Command, null));
        }

        var helpItems = items.Where(item => item.Role == AppMenuRole.Help).Select(item => (UIMenuElement)item.Command).ToArray();
        if (helpItems.Length > 0)
        {
            children.Add(UIMenu.Create(string.Empty, null, UIMenuIdentifier.None, UIMenuOptions.DisplayInline, helpItems));
        }

        if (children.Count == 0)
        {
            // AppKit's own "<app> Help" would only say that no help is available.
            builder.RemoveMenu(UIMenuIdentifier.Help.GetConstant());
            return;
        }

        builder.ReplaceChildrenOfMenu(UIMenuIdentifier.Help.GetConstant(), _ => children.ToArray());
    }

    // File > Close closes the window (Cmd-W); a page with its own Close item (a document tab) takes
    // the shortcut over while it is on screen.
    private static void BuildCloseItem(IUIMenuBuilder builder, RoleItem close)
    {
        if (close.Item is null)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(close.Item.Text) ? "Close" : close.Item.Text;
        var command = CloneWithKey(close.Command, title, "w", UIKeyModifierFlags.Command);
        builder.ReplaceChildrenOfMenu(UIMenuIdentifier.Close.GetConstant(), _ => [command]);
    }

    private readonly record struct RoleItem(AppMenuItem Item, AppMenuRole Role, UICommand Command);

    private static List<RoleItem> CollectRoleItems()
    {
        var result = new List<RoleItem>();
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if ((window as IMenuBarElement)?.MenuBar is not { } menuBar)
        {
            return result;
        }

        foreach (var element in Descendants(menuBar))
        {
            if (element is not AppMenuItem { MacMenuRole: not AppMenuRole.None } item ||
                MenuBarVisibility.PlatformElement(item) is not UICommand command)
            {
                continue;
            }

            // A hidden item stays out of the menus, except Close: its shortcut still closes the
            // current document when the page shows its documents as tabs instead of in the File menu.
            if (!item.IsVisible && item.MacMenuRole != AppMenuRole.Close)
            {
                continue;
            }

            result.Add(new RoleItem(item, item.MacMenuRole, command));
        }

        return result;
    }

    private static IEnumerable<IMenuElement> Descendants(IEnumerable<IMenuElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            if (element is IMenuFlyoutSubItem subItem)
            {
                foreach (var descendant in Descendants(subItem))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static IEnumerable<IMenuElement> Descendants(IMenuBar menuBar)
    {
        foreach (var menuBarItem in menuBar)
        {
            foreach (var descendant in Descendants(menuBarItem))
            {
                yield return descendant;
            }
        }
    }

    // Takes the role items out of the page's menus; a menu left with nothing but separators goes.
    private static void RemoveFromPageMenus(IUIMenuBuilder builder, List<RoleItem> items)
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (items.Count == 0 || (window as IMenuBarElement)?.MenuBar is not { } menuBar)
        {
            return;
        }

        var keys = items.Select(item => Key(item.Command)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var menuBarItem in menuBar)
        {
            if (MenuBarVisibility.PlatformElement(menuBarItem) is not UIMenu menu || RawIdentifier(menu) is not { Length: > 0 } identifier ||
                builder.GetMenu(identifier) is not { } built)
            {
                continue;
            }

            var remaining = Filter(built.Children, keys);
            if (remaining.Length == built.Children.Length)
            {
                continue;
            }

            if (remaining.All(child => child is UIMenu { Options: var options } section && options.HasFlag(UIMenuOptions.DisplayInline) && section.Children.Length == 0))
            {
                builder.RemoveMenu(identifier);
            }
            else
            {
                builder.ReplaceChildrenOfMenu(identifier, _ => remaining);
            }
        }
    }

    private static UIMenuElement[] Filter(UIMenuElement[] children, HashSet<string> keys)
    {
        var result = new List<UIMenuElement>(children.Length);
        foreach (var child in children)
        {
            if (Key(child) is { } key && keys.Contains(key))
            {
                continue;
            }

            if (child is UIMenu submenu)
            {
                var filtered = Filter(submenu.Children, keys);
                if (filtered.Length == 0 && submenu.Options.HasFlag(UIMenuOptions.DisplayInline))
                {
                    continue;
                }

                result.Add(filtered.Length == submenu.Children.Length ? submenu : submenu.GetMenuByReplacingChildren(filtered));
                continue;
            }

            result.Add(child);
        }

        return result.ToArray();
    }

    private static UICommand Clone(UICommand command, string title)
    {
        var clone = UICommand.Create(title, command.Image, MenuItemSelector, command.PropertyList);
        clone.Attributes = command.Attributes;
        return clone;
    }

    private static UIKeyCommand CloneWithKey(UICommand command, string title, string input, UIKeyModifierFlags modifiers)
    {
        var clone = UIKeyCommand.Create(title, command.Image, MenuItemSelector, input, modifiers, command.PropertyList);
        clone.Attributes = command.Attributes;
        return clone;
    }

    private static bool IsOnCurrentMenuBar(AppMenuItem item)
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        return (window as IMenuBarElement)?.MenuBar is { } menuBar && Descendants(menuBar).Contains(item) && item.IsEnabled;
    }

    private static string? Key(UIMenuElement? element) => element is UICommand { PropertyList: NSObject propertyList }
        ? "command:" + propertyList
        : null;

    // UIMenu.Identifier is bound as an enum of the system menus, which cannot carry the
    // identifiers MAUI generates for the app's own menus.
    private static string RawIdentifier(UIMenu menu) => menu.ValueForKey(new NSString("identifier"))?.ToString() ?? string.Empty;
}
