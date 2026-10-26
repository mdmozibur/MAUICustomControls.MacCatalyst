namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

// Inside the namespace, so Avalonia's names win over MAUI's global usings.
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Layout;
using MAUICustomControls.MacCatalyst.Controls;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;

/// <summary>
/// The page's MenuBarItems as an Avalonia menu bar in the window, where UWP's MenuBar was. Each
/// menu is built when it opens, so hidden items stay out and texts and enabled states are current,
/// as the Mac's menu bar rebuilds them.
/// </summary>
public sealed class AppMenuBarHostHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<AppMenuBarHost, Menu>
{
    public static readonly PropertyMapper<AppMenuBarHost, AppMenuBarHostHandler> Mapper = new(AvaloniaViewHandler.ViewMapper);

    private Microsoft.Maui.Controls.Page? _page;

    public AppMenuBarHostHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override Menu CreatePlatformView() => new()
    {
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    protected override void ConnectHandler(Menu platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.ParentChanged += OnParentChanged;
        AttachPage();
    }

    protected override void DisconnectHandler(Menu platformView)
    {
        VirtualView.ParentChanged -= OnParentChanged;
        DetachPage();
        platformView.Items.Clear();
        base.DisconnectHandler(platformView);
    }

    private void OnParentChanged(object? sender, EventArgs e)
    {
        DetachPage();
        AttachPage();
    }

    private void AttachPage()
    {
        Microsoft.Maui.Controls.Element? element = VirtualView;
        while (element is not null and not Microsoft.Maui.Controls.Page)
        {
            element = element.Parent;
        }

        _page = element as Microsoft.Maui.Controls.Page;
        if (_page?.MenuBarItems is INotifyCollectionChanged items)
        {
            items.CollectionChanged += OnMenuBarItemsChanged;
        }

        Rebuild();
    }

    private void DetachPage()
    {
        if (_page?.MenuBarItems is INotifyCollectionChanged items)
        {
            items.CollectionChanged -= OnMenuBarItemsChanged;
        }

        _page = null;
    }

    private void OnMenuBarItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        PlatformView.Items.Clear();
        if (_page is null)
        {
            return;
        }

        foreach (var bar in _page.MenuBarItems)
        {
            if (MenuVisibility.IsHidden(bar))
            {
                continue;
            }

            var top = new MenuItem { Header = bar.Text, IsEnabled = bar.IsEnabled };
            var source = bar;
            Fill(top, source);
            // Built again on opening: the app changes items while the menu is closed.
            top.SubmenuOpened += (_, e) =>
            {
                if (ReferenceEquals(e.Source, top))
                {
                    Fill(top, source);
                }
            };
            PlatformView.Items.Add(top);
        }
    }

    private void Fill(MenuItem top, Microsoft.Maui.Controls.MenuBarItem source)
    {
        top.Items.Clear();
        foreach (var item in MenuPresenter.BuildItems(source, MauiContext))
        {
            top.Items.Add(item);
        }
    }
}
