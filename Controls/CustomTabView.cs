using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

public class CustomTabView : TemplatedView
{
    private const double WidthPerScroll = 100;
    private const string AddButtonVisibleStateName = "AddButtonVisible";
    private const string AddButtonHiddenStateName = "AddButtonHidden";
    private const string ScrollButtonsVisibleStateName = "ScrollButtonsVisible";
    private const string ScrollButtonsHiddenStateName = "ScrollButtonsHidden";
    private const string ScrollButtonsInactiveStateName = "ScrollButtonsInactive";
    private const string ScrollButtonsAtStartStateName = "ScrollButtonsAtStart";
    private const string ScrollButtonsInMiddleStateName = "ScrollButtonsInMiddle";
    private const string ScrollButtonsAtEndStateName = "ScrollButtonsAtEnd";

    private static readonly ConstructorInfo? SelectionChangedEventArgsConstructor =
        typeof(SelectionChangedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(object), typeof(object) },
            modifiers: null);

    private readonly ObservableCollection<object> _items = new();
    private Grid? _chromeRoot;
    private Button? _leftScrollButton;
    private Button? _rightScrollButton;
    private Button? _addButton;
    private ScrollView? _scrollView;
    private HorizontalStackLayout? _tabHost;

    public event EventHandler<EventArgs>? AddButtonClick;

    public event EventHandler<TabItem>? CloseButtonClick;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public static readonly BindableProperty AddButtonVisibilityProperty =
        BindableProperty.Create(nameof(AddButtonVisibility), typeof(bool), typeof(CustomTabView), true, propertyChanged: OnChromePropertyChanged);

    public static readonly BindableProperty ScrollButtonsVisibilityProperty =
        BindableProperty.Create(nameof(ScrollButtonsVisibility), typeof(bool), typeof(CustomTabView), true, propertyChanged: OnChromePropertyChanged);

    public static readonly BindableProperty ButtonsPaddingProperty =
        BindableProperty.Create(nameof(ButtonsPadding), typeof(Thickness), typeof(CustomTabView), new Thickness(4), propertyChanged: OnChromePropertyChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(CustomTabView), null, propertyChanged: OnSelectedItemChanged);

    public bool AddButtonVisibility
    {
        get => (bool)GetValue(AddButtonVisibilityProperty);
        set => SetValue(AddButtonVisibilityProperty, value);
    }

    public bool ScrollButtonsVisibility
    {
        get => (bool)GetValue(ScrollButtonsVisibilityProperty);
        set => SetValue(ScrollButtonsVisibilityProperty, value);
    }

    public Thickness ButtonsPadding
    {
        get => (Thickness)GetValue(ButtonsPaddingProperty);
        set => SetValue(ButtonsPaddingProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IList<object> Items => _items;

    public CustomTabView()
    {
        if (Application.Current?.Resources.TryGetValue("CustomTabViewControlTemplate", out var template) == true
            && template is ControlTemplate ct)
        {
            ControlTemplate = ct;
        }
        _items.CollectionChanged += Items_CollectionChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_leftScrollButton is not null) _leftScrollButton.Clicked -= LeftScrollButton_Click;
        if (_rightScrollButton is not null) _rightScrollButton.Clicked -= RightScrollButton_Click;
        if (_addButton is not null) _addButton.Clicked -= AddButton_Click;
        if (_scrollView is not null)
        {
            _scrollView.SizeChanged -= ScrollView_SizeChanged;
            _scrollView.Scrolled -= ScrollView_Scrolled;
        }
        if (_tabHost is not null) _tabHost.SizeChanged -= TabHost_SizeChanged;

        _chromeRoot = GetTemplateChild("TabViewChromeRoot") as Grid;
        _leftScrollButton = GetTemplateChild("LeftScrollButton") as Button;
        _rightScrollButton = GetTemplateChild("RightScrollButton") as Button;
        _addButton = GetTemplateChild("AddButton") as Button;
        _scrollView = GetTemplateChild("TabScrollView") as ScrollView;
        _tabHost = GetTemplateChild("TabHost") as HorizontalStackLayout;

        if (_leftScrollButton is not null) _leftScrollButton.Clicked += LeftScrollButton_Click;
        if (_rightScrollButton is not null) _rightScrollButton.Clicked += RightScrollButton_Click;
        if (_addButton is not null) _addButton.Clicked += AddButton_Click;
        if (_scrollView is not null)
        {
            _scrollView.SizeChanged += ScrollView_SizeChanged;
            _scrollView.Scrolled += ScrollView_Scrolled;
        }
        if (_tabHost is not null) _tabHost.SizeChanged += TabHost_SizeChanged;

        RebuildTabs();
        UpdateVisualStates();
    }

    private void TabHost_SizeChanged(object? sender, EventArgs e) => UpdateVisualStates();
    private void ScrollView_SizeChanged(object? sender, EventArgs e) => UpdateVisualStates();
    private void ScrollView_Scrolled(object? sender, ScrolledEventArgs e) => UpdateVisualStates();

    public IList<object> GetItems()
    {
        return _items;
    }

    public int GetSelectedIndex()
    {
        return SelectedItem is null ? -1 : _items.IndexOf(SelectedItem);
    }

    public void SetSelectedIndex(int value)
    {
        SelectedItem = value >= 0 && value < _items.Count ? _items[value] : null;
    }

    protected virtual TabItem CreateTabItem()
    {
        return new TabItem
        {
            Title = "Custom Title"
        };
    }

    protected virtual void ConfigureTabItem(TabItem tabItem, object item)
    {
        tabItem.BindingContext = item;
        tabItem.Title = "Tab " + _items.IndexOf(item);

        tabItem.IsSelected = Equals(item, SelectedItem);
    }

    private static void OnChromePropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((CustomTabView)bindable).UpdateVisualStates();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((CustomTabView)bindable).HandleSelectedItemChanged(oldValue, newValue);
    }

#pragma warning disable CS8602
    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        var selectedItem = SelectedItem;
        if (selectedItem is not null && !_items.Contains(selectedItem))
        {
            SetValue(SelectedItemProperty, null);
        }

        if (e is null)
            return;

        var args = e!;
        if (_tabHost is not HorizontalStackLayout tabHost)
            return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertTabs(tabHost, args.NewStartingIndex, args.NewItems!);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveTabs(tabHost, args.OldStartingIndex, args.OldItems!.Count);
                break;
            case NotifyCollectionChangedAction.Replace:
                RemoveTabs(tabHost, args.OldStartingIndex, args.OldItems!.Count);
                InsertTabs(tabHost, args.NewStartingIndex, args.NewItems!);
                break;
            case NotifyCollectionChangedAction.Move:
                var movedTab = tabHost.Children[args.OldStartingIndex];
                tabHost.Children.RemoveAt(args.OldStartingIndex);
                tabHost.Children.Insert(args.NewStartingIndex, movedTab);
                break;
            default: // Reset
                RebuildTabs();
                break;
        }

        UpdateVisualStates();
    }
#pragma warning restore CS8602

    private void InsertTabs(HorizontalStackLayout tabHost, int startIndex, System.Collections.IList items)
    {
        int index = startIndex;
        foreach (var item in items)
        {
            var tabItem = CreateTabItem();
            ConfigureTabItem(tabItem, item);
            tabItem.SelectionRequested += TabItem_SelectionRequested;
            tabItem.CloseRequested += TabItem_CloseRequested;
            tabHost.Children.Insert(index++, tabItem);
        }
    }

    private void RemoveTabs(HorizontalStackLayout tabHost, int startIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (startIndex < tabHost.Children.Count && tabHost.Children[startIndex] is TabItem tab)
            {
                tab.SelectionRequested -= TabItem_SelectionRequested;
                tab.CloseRequested -= TabItem_CloseRequested;
                tabHost.Children.RemoveAt(startIndex);
            }
        }
    }

    private void HandleSelectedItemChanged(object? oldValue, object? newValue)
    {
        UpdateTabSelectionStates();

        if (SelectionChangedEventArgsConstructor is not null)
        {
            var args = (SelectionChangedEventArgs)SelectionChangedEventArgsConstructor.Invoke(new[] { oldValue, newValue });
            SelectionChanged?.Invoke(this, args);
        }

        ScrollSelectedTabIntoView();
        UpdateVisualStates();
    }

    private async void ScrollSelectedTabIntoView()
    {
        if (SelectedItem is null || _tabHost is null || _scrollView is null)
            return;

        foreach (var child in _tabHost.Children.OfType<TabItem>())
        {
            if (Equals(child.BindingContext, SelectedItem))
            {
                // Give layout a frame to settle
                await Task.Yield();
                var targetX = child.X;
                var tabWidth = child.Width;
                var viewportWidth = _scrollView.Width;
                var scrollX = _scrollView.ScrollX;

                if (targetX < scrollX)
                {
                    await _scrollView.ScrollToAsync(targetX, 0, true);
                }
                else if (targetX + tabWidth > scrollX + viewportWidth)
                {
                    await _scrollView.ScrollToAsync(targetX + tabWidth - viewportWidth, 0, true);
                }
                break;
            }
        }
    }

    private void RebuildTabs()
    {
        if (_tabHost is null) return;

        foreach (var existingTab in _tabHost.Children.OfType<TabItem>().ToArray())
        {
            existingTab.SelectionRequested -= TabItem_SelectionRequested;
            existingTab.CloseRequested -= TabItem_CloseRequested;
        }

        _tabHost.Children.Clear();
        foreach (var item in _items)
        {
            var tabItem = CreateTabItem();
            ConfigureTabItem(tabItem, item);
            tabItem.SelectionRequested += TabItem_SelectionRequested;
            tabItem.CloseRequested += TabItem_CloseRequested;
            _tabHost.Children.Add(tabItem);
        }

        UpdateTabSelectionStates();
        UpdateVisualStates();
    }

    private void UpdateTabSelectionStates()
    {
        if (_tabHost is null) return;
        foreach (var tabItem in _tabHost.Children.OfType<TabItem>())
        {
            tabItem.IsSelected = Equals(tabItem.BindingContext, SelectedItem);
        }
    }

    private void UpdateVisualStates()
    {
        if (_chromeRoot is null)
            return;

        VisualStateManager.GoToState(_chromeRoot, GetAddButtonStateName());
        VisualStateManager.GoToState(_chromeRoot, GetScrollButtonsVisibilityStateName());
        VisualStateManager.GoToState(_chromeRoot, GetScrollButtonsAvailabilityStateName());
    }

    private string GetAddButtonStateName()
    {
        return AddButtonVisibility ? AddButtonVisibleStateName : AddButtonHiddenStateName;
    }

    private string GetScrollButtonsVisibilityStateName()
    {
        return CanShowScrollButtons() ? ScrollButtonsVisibleStateName : ScrollButtonsHiddenStateName;
    }

    private string GetScrollButtonsAvailabilityStateName()
    {
        if (!CanShowScrollButtons() || _scrollView is null)
            return ScrollButtonsInactiveStateName;

        bool canScrollLeft = _scrollView.ScrollX > 0;
        bool canScrollRight = _tabHost is not null && _scrollView.ScrollX + _scrollView.Width < _tabHost.Width - 1;

        if (canScrollLeft && canScrollRight)
            return ScrollButtonsInMiddleStateName;

        if (canScrollLeft)
            return ScrollButtonsAtEndStateName;

        if (canScrollRight)
            return ScrollButtonsAtStartStateName;

        return ScrollButtonsInactiveStateName;
    }

    private bool CanShowScrollButtons()
    {
        return _tabHost is not null
            && _scrollView is not null
            && ScrollButtonsVisibility
            && _tabHost.Width > _scrollView.Width + 1;
    }

    private void TabItem_SelectionRequested(object? sender, EventArgs e)
    {
        if (sender is TabItem tabItem)
        {
            SelectedItem = tabItem.BindingContext;
        }
    }

    private void TabItem_CloseRequested(object? sender, EventArgs e)
    {
        if (sender is TabItem tabItem)
        {
            CloseButtonClick?.Invoke(this, tabItem);
        }
    }

    private async void LeftScrollButton_Click(object? sender, EventArgs e)
    {
        if (_scrollView is null) return;
        await _scrollView.ScrollToAsync(Math.Max(0, _scrollView.ScrollX - WidthPerScroll), 0, true);
        UpdateVisualStates();
    }

    private async void RightScrollButton_Click(object? sender, EventArgs e)
    {
        if (_scrollView is null || _tabHost is null) return;
        var maxOffset = Math.Max(0, _tabHost.Width - _scrollView.Width);
        await _scrollView.ScrollToAsync(Math.Min(maxOffset, _scrollView.ScrollX + WidthPerScroll), 0, true);
        UpdateVisualStates();
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        AddButtonClick?.Invoke(this, e);
    }
}
