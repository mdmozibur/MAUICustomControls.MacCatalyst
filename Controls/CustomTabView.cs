using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MAUICustomControls.MacCatalyst.Controls;

public class CustomTabView : TemplatedView
{
    private const double WidthPerScroll = 100;

    private static readonly ConstructorInfo? SelectionChangedEventArgsConstructor =
        typeof(SelectionChangedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(object), typeof(object) },
            modifiers: null);

    private readonly ObservableCollection<object> _items = new();
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
        UpdateChrome();
    }

    private void TabHost_SizeChanged(object? sender, EventArgs e) => UpdateScrollButtonVisibility();
    private void ScrollView_SizeChanged(object? sender, EventArgs e) => UpdateScrollButtonVisibility();
    private void ScrollView_Scrolled(object? sender, ScrolledEventArgs e) => UpdateScrollButtonVisibility();

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
        ((CustomTabView)bindable).UpdateChrome();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((CustomTabView)bindable).HandleSelectedItemChanged(oldValue, newValue);
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedItem != null && !_items.Contains(SelectedItem))
        {
            SetValue(SelectedItemProperty, null);
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertTabs(e.NewStartingIndex, e.NewItems!);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveTabs(e.OldStartingIndex, e.OldItems!.Count);
                break;
            case NotifyCollectionChangedAction.Replace:
                RemoveTabs(e.OldStartingIndex, e.OldItems!.Count);
                InsertTabs(e.NewStartingIndex, e.NewItems!);
                break;
            case NotifyCollectionChangedAction.Move:
                var movedTab = _tabHost.Children[e.OldStartingIndex];
                _tabHost.Children.RemoveAt(e.OldStartingIndex);
                _tabHost.Children.Insert(e.NewStartingIndex, movedTab);
                break;
            default: // Reset
                RebuildTabs();
                break;
        }

        UpdateScrollButtonVisibility();
    }

    private void InsertTabs(int startIndex, System.Collections.IList items)
    {
        if (_tabHost is null) return;
        int index = startIndex;
        foreach (var item in items)
        {
            var tabItem = CreateTabItem();
            ConfigureTabItem(tabItem, item);
            tabItem.SelectionRequested += TabItem_SelectionRequested;
            tabItem.CloseRequested += TabItem_CloseRequested;
            _tabHost.Children.Insert(index++, tabItem);
        }
    }

    private void RemoveTabs(int startIndex, int count)
    {
        if (_tabHost is null) return;
        for (int i = 0; i < count; i++)
        {
            if (startIndex < _tabHost.Children.Count && _tabHost.Children[startIndex] is TabItem tab)
            {
                tab.SelectionRequested -= TabItem_SelectionRequested;
                tab.CloseRequested -= TabItem_CloseRequested;
                _tabHost.Children.RemoveAt(startIndex);
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
        UpdateScrollButtonVisibility();
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
        UpdateScrollButtonVisibility();
    }

    private void UpdateTabSelectionStates()
    {
        if (_tabHost is null) return;
        foreach (var tabItem in _tabHost.Children.OfType<TabItem>())
        {
            tabItem.IsSelected = Equals(tabItem.BindingContext, SelectedItem);
        }
    }

    private void UpdateChrome()
    {
        if (_leftScrollButton is not null) _leftScrollButton.Padding = ButtonsPadding;
        if (_rightScrollButton is not null) _rightScrollButton.Padding = ButtonsPadding;
        if (_addButton is not null)
        {
            _addButton.Padding = ButtonsPadding;
            _addButton.IsVisible = AddButtonVisibility;
        }
        UpdateScrollButtonVisibility();
    }

    private void UpdateScrollButtonVisibility()
    {
        if (_tabHost is null || _scrollView is null || _leftScrollButton is null || _rightScrollButton is null) return;
        var showScrollButtons = ScrollButtonsVisibility && _tabHost.Width > _scrollView.Width + 1;
        _leftScrollButton.IsVisible = showScrollButtons;
        _rightScrollButton.IsVisible = showScrollButtons;
        _leftScrollButton.IsEnabled = showScrollButtons && _scrollView.ScrollX > 0;
        _rightScrollButton.IsEnabled = showScrollButtons && _scrollView.ScrollX + _scrollView.Width < _tabHost.Width - 1;
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
        UpdateScrollButtonVisibility();
    }

    private async void RightScrollButton_Click(object? sender, EventArgs e)
    {
        if (_scrollView is null || _tabHost is null) return;
        var maxOffset = Math.Max(0, _tabHost.Width - _scrollView.Width);
        await _scrollView.ScrollToAsync(Math.Min(maxOffset, _scrollView.ScrollX + WidthPerScroll), 0, true);
        UpdateScrollButtonVisibility();
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        AddButtonClick?.Invoke(this, e);
    }
}
