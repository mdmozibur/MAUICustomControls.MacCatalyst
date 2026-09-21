namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A context menu with XAML's <c>Opening</c> and <c>Closed</c> events. Opening is raised just before
/// the menu is built, so a handler can still show, hide, enable or disable its items.
/// </summary>
public class ContextMenuFlyout : MenuFlyout
{
    public event EventHandler<object>? Opening;

    public event EventHandler<object>? Closed;

    internal void RaiseOpening() => Opening?.Invoke(this, EventArgs.Empty);

    internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
}

/// <summary>A menu element that can be left out of its menu, which MAUI's menu elements cannot.</summary>
public interface IHideableMenuElement
{
    /// <summary>False leaves the element out of the menu (unlike IsEnabled, which greys it out).</summary>
    bool IsVisible { get; }
}

/// <summary>
/// Where a menu bar item goes among the menus macOS apps share (the page's own menus hold the rest).
/// </summary>
public enum AppMenuRole
{
    /// <summary>Stays in its own menu.</summary>
    None,

    /// <summary>The application menu's About item.</summary>
    About,

    /// <summary>The application menu's Settings… item (Cmd-,).</summary>
    Settings,

    /// <summary>The application menu, after Settings.</summary>
    Application,

    /// <summary>The Help menu, after the app's Help item.</summary>
    Help,

    /// <summary>The Help menu's "&lt;app&gt; Help" item (Cmd-?).</summary>
    StandardHelp,

    /// <summary>File > Close (Cmd-W), in place of closing the window.</summary>
    Close,
}

/// <summary>MenuFlyoutItem with <see cref="IsVisible"/>, for context menus and the menu bar.</summary>
public class AppMenuItem : MenuFlyoutItem, IHideableMenuElement
{
    public static readonly BindableProperty IsVisibleProperty = MenuVisibility.CreateProperty(typeof(AppMenuItem));

    public static readonly BindableProperty MacMenuRoleProperty = BindableProperty.Create(
        nameof(MacMenuRole), typeof(AppMenuRole), typeof(AppMenuItem), AppMenuRole.None, propertyChanged: MenuVisibility.OnMenuChanged);

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>Moves a menu bar item to the standard macOS place for it (see <see cref="AppMenuRole"/>).</summary>
    public AppMenuRole MacMenuRole
    {
        get => (AppMenuRole)GetValue(MacMenuRoleProperty);
        set => SetValue(MacMenuRoleProperty, value);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // A moved item is a copy under its standard title (File > Close shows the item's own text),
        // so its menu is rebuilt when the text or the enabled state changes.
        if (MacMenuRole != AppMenuRole.None && propertyName is nameof(Text) or nameof(IsEnabled))
        {
            MenuVisibility.OnMenuChanged(this, null!, null!);
        }
    }
}

/// <summary>MenuFlyoutSubItem with <see cref="IsVisible"/>.</summary>
public class AppMenuSubItem : MenuFlyoutSubItem, IHideableMenuElement
{
    public static readonly BindableProperty IsVisibleProperty = MenuVisibility.CreateProperty(typeof(AppMenuSubItem));

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
}

/// <summary>MenuFlyoutSeparator with <see cref="IsVisible"/>.</summary>
public class AppMenuSeparator : MenuFlyoutSeparator, IHideableMenuElement
{
    public static readonly BindableProperty IsVisibleProperty = MenuVisibility.CreateProperty(typeof(AppMenuSeparator));

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
}

/// <summary>MenuBarItem (a top-level menu) with <see cref="IsVisible"/>.</summary>
public class AppMenuBarItem : MenuBarItem, IHideableMenuElement
{
    public static readonly BindableProperty IsVisibleProperty = MenuVisibility.CreateProperty(typeof(AppMenuBarItem));

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
}

internal static class MenuVisibility
{
    public static BindableProperty CreateProperty(Type owner) =>
        BindableProperty.Create("IsVisible", typeof(bool), owner, true, propertyChanged: OnIsVisibleChanged);

    public static bool IsHidden(object? element) => element is IHideableMenuElement { IsVisible: false };

    internal static void OnMenuChanged(BindableObject bindable, object oldValue, object newValue) =>
        OnIsVisibleChanged(bindable, oldValue, newValue);

    private static void OnIsVisibleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        _ = oldValue;
        _ = newValue;

#if MACCATALYST || IOS
        // Context menus are built each time they open; the menu bar is built once and has to be
        // rebuilt, which is when hidden items are left out (see MenuBarVisibility).
        if (IsInMenuBar(bindable as Element))
        {
            UIKit.UIMenuSystem.MainSystem.SetNeedsRebuild();
        }
#endif
    }

    private static bool IsInMenuBar(Element? element)
    {
        for (; element is not null; element = element.Parent)
        {
            if (element is MenuBarItem)
            {
                return true;
            }
        }

        return false;
    }
}
