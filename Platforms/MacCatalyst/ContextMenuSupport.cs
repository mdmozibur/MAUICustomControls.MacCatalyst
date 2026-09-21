using CoreGraphics;
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Native context menus for <c>FlyoutBase.ContextFlyout</c>, replacing MAUI's mapping: the menu is
/// built when it opens, after <see cref="ContextMenuFlyout.Opening"/> has run, and leaves out
/// items hidden with <see cref="IHideableMenuElement.IsVisible"/>. Separators become menu
/// sections, so none is ever doubled or left dangling at an end.
/// </summary>
public static class ContextMenuSupport
{
    private const string MappingKey = nameof(IContextFlyoutElement.ContextFlyout);

    public static void Configure()
    {
        ViewHandler.ViewMapper.ModifyMapping<IView, IViewHandler>(MappingKey, (handler, view, _) => MapContextFlyout(handler, view));
        MenuBarVisibility.TrackPlatformElements();
    }

    private static void MapContextFlyout(IViewHandler handler, IView view)
    {
        if (handler.PlatformView is not UIView platformView || handler.MauiContext is not { } mauiContext)
        {
            return;
        }

        // Only the interaction added here is replaced. Views keep their own: a UIButton presents its
        // Menu through a UIContextMenuInteraction, and removing that one made ToggleDropdown's option
        // menu open only after a long delay.
        foreach (var interaction in platformView.Interactions.OfType<ContextFlyoutInteraction>().ToArray())
        {
            platformView.RemoveInteraction(interaction);
        }

        if (view is IContextFlyoutElement { ContextFlyout: IMenuFlyout })
        {
            platformView.AddInteraction(new ContextFlyoutInteraction(new ContextMenuDelegate(view, mauiContext)));
        }
    }

    /// <summary>Marks the context-menu interaction this class adds, so it never touches others.</summary>
    private sealed class ContextFlyoutInteraction : UIContextMenuInteraction
    {
        public ContextFlyoutInteraction(IUIContextMenuInteractionDelegate interactionDelegate)
            : base(interactionDelegate)
        {
        }
    }

    /// <summary>Builds the menu's children; hidden items are skipped and separators start a new section.</summary>
    internal static UIMenuElement[] BuildMenuElements(IEnumerable<IMenuElement> items, IMauiContext mauiContext)
    {
        var sections = new List<List<UIMenuElement>>();
        var current = new List<UIMenuElement>();

        foreach (var item in items)
        {
            if (MenuVisibility.IsHidden(item))
            {
                continue;
            }

            switch (item)
            {
                case IMenuFlyoutSeparator:
                    if (current.Count > 0)
                    {
                        sections.Add(current);
                        current = new List<UIMenuElement>();
                    }

                    break;

                case IMenuFlyoutSubItem subItem:
                {
                    var children = BuildMenuElements(subItem, mauiContext);
                    if (children.Length > 0)
                    {
                        current.Add(UIMenu.Create(subItem.Text ?? string.Empty, LoadIcon(subItem.Source, mauiContext), UIMenuIdentifier.None, 0, children));
                    }

                    break;
                }

                default:
                {
                    var element = item;
                    var action = UIAction.Create(item.Text ?? string.Empty, LoadIcon(item.Source, mauiContext), null, _ => element.Clicked());
                    if (!item.IsEnabled)
                    {
                        action.Attributes = UIMenuElementAttributes.Disabled;
                    }

                    if (item is BindableObject menuItem && SemanticProperties.GetDescription(menuItem) is { Length: > 0 } description)
                    {
                        action.DiscoverabilityTitle = description;
                    }

                    current.Add(action);
                    break;
                }
            }
        }

        if (current.Count > 0)
        {
            sections.Add(current);
        }

        if (sections.Count <= 1)
        {
            return sections.FirstOrDefault()?.ToArray() ?? [];
        }

        return sections
            .Select(section => (UIMenuElement)UIMenu.Create(string.Empty, null, UIMenuIdentifier.None, UIMenuOptions.DisplayInline, section.ToArray()))
            .ToArray();
    }

    /// <summary>A menu icon, drawn as a template image so the menu tints it for its state and theme.</summary>
    internal static UIImage? LoadIcon(IImageSource? source, IMauiContext mauiContext)
    {
        switch (source)
        {
            case IFontImageSource { Glyph.Length: > 0 } fontSource:
            {
                var fontManager = mauiContext.Services.GetService(typeof(IFontManager)) as IFontManager;
                // Menu icons have a fixed size on the Mac; the XAML size was chosen for a UWP menu.
                var font = fontManager?.GetFont(fontSource.Font.WithSize(MenuIconPointSize)) ?? UIFont.SystemFontOfSize((nfloat)MenuIconPointSize);
                var text = new NSAttributedString(fontSource.Glyph, new UIStringAttributes { Font = font, ForegroundColor = UIColor.Black });
                var size = text.Size;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    return null;
                }

                var renderer = new UIGraphicsImageRenderer(new CGSize(Math.Ceiling((double)size.Width), Math.Ceiling((double)size.Height)));
                return renderer.CreateImage(_ => text.DrawString(CGPoint.Empty)).ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
            }

            case IFileImageSource { File.Length: > 0 } fileSource:
                return UIImage.FromBundle(fileSource.File) ?? UIImage.GetSystemImage(fileSource.File);

            default:
                return null;
        }
    }

    private const double MenuIconPointSize = 13;

    private sealed class ContextMenuDelegate : UIContextMenuInteractionDelegate
    {
        private readonly WeakReference<IView> _view;
        private readonly IMauiContext _mauiContext;
        private IMenuFlyout? _openFlyout;

        public ContextMenuDelegate(IView view, IMauiContext mauiContext)
        {
            _view = new WeakReference<IView>(view);
            _mauiContext = mauiContext;
        }

        public override UIContextMenuConfiguration? GetConfigurationForMenu(UIContextMenuInteraction interaction, CGPoint location)
        {
            if (!_view.TryGetTarget(out var view) ||
                view is not IContextFlyoutElement { ContextFlyout: IMenuFlyout flyout } ||
                view is VisualElement { IsEnabled: false })
            {
                return null;
            }

            // UWP raises Opening before showing the menu; handlers adjust the items from the
            // current selection. Build afterwards so the menu reflects their changes.
            (flyout as ContextMenuFlyout)?.RaiseOpening();

            var elements = BuildMenuElements(flyout, _mauiContext);
            if (elements.Length == 0)
            {
                // Every item hidden: UWP shows nothing (and raises no Closed).
                return null;
            }

            _openFlyout = flyout;
            var menu = UIMenu.Create(string.Empty, null, UIMenuIdentifier.None, 0, elements);
            return UIContextMenuConfiguration.Create(null, null, _ => menu);
        }

        public override void WillEnd(UIContextMenuInteraction interaction, UIContextMenuConfiguration configuration, IUIContextMenuInteractionAnimating? animator)
        {
            var flyout = _openFlyout as ContextMenuFlyout;
            _openFlyout = null;
            if (flyout is null)
            {
                return;
            }

            // Closed follows the chosen item's action, as in UWP.
            if (animator is not null)
            {
                animator.AddCompletion(flyout.RaiseClosed);
            }
            else
            {
                flyout.RaiseClosed();
            }
        }
    }
}

/// <summary>
/// Leaves hidden items (<see cref="IHideableMenuElement.IsVisible"/>) out of the menu bar. Call
/// from the app delegate's BuildMenu after MAUI has added its menus; a visibility change asks the
/// system to rebuild the menu bar, which runs it again.
/// </summary>
public static class MenuBarVisibility
{
    // MAUI disconnects the menu bar's handlers as soon as it has added the menus to the builder
    // (WindowHandler.MapMenuBar), so by the time the app delegate's BuildMenu continues, no menu
    // element has a Handler. Each one's platform element is recorded as its handler connects.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IElement, UIMenuElement> PlatformElements = new();

    internal static void TrackPlatformElements()
    {
        MenuBarItemHandler.Mapper.AppendToMapping("TrackPlatformElement", (handler, view) => Track(view, handler.PlatformView));
        MenuFlyoutSubItemHandler.Mapper.AppendToMapping("TrackPlatformElement", (handler, view) => Track(view, handler.PlatformView));
        MenuFlyoutItemHandler.Mapper.AppendToMapping("TrackPlatformElement", (handler, view) => Track(view, handler.PlatformView));
    }

    private static void Track(IElement element, UIMenuElement? platformElement)
    {
        if (platformElement is not null)
        {
            PlatformElements.AddOrUpdate(element, platformElement);
        }
    }

    /// <summary>The UIMenu/UICommand MAUI last built for a menu bar element.</summary>
    public static UIMenuElement? PlatformElement(IElement element) =>
        element.Handler?.PlatformView as UIMenuElement ?? (PlatformElements.TryGetValue(element, out var platformElement) ? platformElement : null);

    public static void RemoveHiddenItems(IUIMenuBuilder builder)
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if ((window as IMenuBarElement)?.MenuBar is not { } menuBar)
        {
            return;
        }

        foreach (var menuBarItem in menuBar)
        {
            if (PlatformElement(menuBarItem) is not UIMenu menu)
            {
                continue;
            }

            if (MenuVisibility.IsHidden(menuBarItem))
            {
                builder.RemoveMenu(RawIdentifier(menu));
                continue;
            }

            var hidden = new HashSet<string>(StringComparer.Ordinal);
            CollectHidden(menuBarItem, hidden);
            if (hidden.Count > 0)
            {
                builder.ReplaceChildrenOfMenu(RawIdentifier(menu), children => Filter(children, hidden));
            }
        }
    }

    // MAUI identifies each menu bar command by its property list (an index it assigns) and each
    // submenu by its identifier; those are what survive into the menu the system builds.
    private static void CollectHidden(IEnumerable<IMenuElement> items, HashSet<string> hidden)
    {
        foreach (var item in items)
        {
            var isHidden = MenuVisibility.IsHidden(item);
            if (isHidden && Key(PlatformElement(item)) is { } key)
            {
                hidden.Add(key);
                continue;
            }

            if (item is IMenuFlyoutSubItem subItem)
            {
                CollectHidden(subItem, hidden);
            }
        }
    }

    private static UIMenuElement[] Filter(UIMenuElement[] children, HashSet<string> hidden)
    {
        var result = new List<UIMenuElement>(children.Length);
        foreach (var child in children)
        {
            if (Key(child) is { } key && hidden.Contains(key))
            {
                continue;
            }

            if (child is UIMenu submenu)
            {
                var filtered = Filter(submenu.Children, hidden);
                // A section (MAUI's separators) left empty would draw a stray separator.
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

    private static string? Key(UIMenuElement? element) => element switch
    {
        UICommand { PropertyList: NSObject propertyList } => "command:" + propertyList,
        UIMenu menu when RawIdentifier(menu) is { Length: > 0 } identifier => "menu:" + identifier,
        _ => null,
    };

    // UIMenu.Identifier is bound as an enum of the system menus, which cannot carry the
    // identifiers MAUI generates for the app's own menus.
    private static string RawIdentifier(UIMenu menu) => menu.ValueForKey(new NSString("identifier"))?.ToString() ?? string.Empty;
}
