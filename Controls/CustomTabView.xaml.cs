using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

public partial class CustomTabView : Grid
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
    private string? _tabFocusGroup;

    public event EventHandler<EventArgs>? AddButtonClick;

    public event EventHandler<TabItem>? CloseButtonClick;

    // Raised when a tab's duplicate action is requested (UWP TabListView.DuplicateRequested).
    public event EventHandler<TabItem>? DuplicateRequested;

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

    // Tracks whether each tab's shapelist can offer "copy file path" (UWP TabListView.AddShapelist).
    private readonly Dictionary<object, bool> _filePathCopyAvailability = new();

    // Adds a shapelist view-model as a tab (typed as object to avoid a view-model project reference).
    public void AddShapelist(object model, bool canCopyFilePath)
    {
        if (model is null || _items.Contains(model))
        {
            return;
        }

        _filePathCopyAvailability[model] = canCopyFilePath;
        _items.Add(model);
    }

    public bool CanCopyFilePath(object model) =>
        model is not null && _filePathCopyAvailability.TryGetValue(model, out var canCopy) && canCopy;

    public CustomTabView()
    {
        InitializeComponent();

        _items.CollectionChanged += Items_CollectionChanged;

        LeftScrollButton.Clicked += LeftScrollButton_Click;
        RightScrollButton.Clicked += RightScrollButton_Click;
        AddButton.Clicked += AddButton_Click;
        TabScrollView.SizeChanged += ScrollView_SizeChanged;
        TabScrollView.Scrolled += ScrollView_Scrolled;
        TabHost.SizeChanged += TabHost_SizeChanged;
#if MACCATALYST || IOS
        // The strip usually sits in the window's title bar area; UIKit would push its content down
        // by the (unsafe) title bar inset. A horizontal tab strip has no inset to respect...
        TabScrollView.HandlerChanged += (_, _) =>
        {
            if (TabScrollView.Handler?.PlatformView is UIKit.UIScrollView scrollView)
            {
                scrollView.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;

                // Nor the soft edge (macOS 26) that blurs scrolled content under a title bar: the tabs
                // are the title bar.
                if (OperatingSystem.IsMacCatalystVersionAtLeast(26) || OperatingSystem.IsIOSVersionAtLeast(26))
                {
                    scrollView.TopEdgeEffect.Hidden = true;
                    scrollView.BottomEdgeEffect.Hidden = true;
                    scrollView.LeftEdgeEffect.Hidden = true;
                    scrollView.RightEdgeEffect.Hidden = true;
                }
            }
        };
#endif

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
        return new TabItem();
    }

    protected virtual void ConfigureTabItem(TabItem tabItem, object item)
    {
        tabItem.BindingContext = item;
        tabItem.Title = "Tab " + _items.IndexOf(item);
        tabItem.CanCopyFilePath = CanCopyFilePath(item);

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

        // UWP's tab strip (Reorder/Content/Entrance item transitions): tabs that shift glide from
        // where they were, a new tab fades and slides in. A reset rebuilds without motion.
        if (args.Action != NotifyCollectionChangedAction.Reset)
        {
            TabMotion.CapturePositions(TabHost);
        }

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertTabs(TabHost, args.NewStartingIndex, args.NewItems!);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveTabs(TabHost, args.OldStartingIndex, args.OldItems!.Count);
                break;
            case NotifyCollectionChangedAction.Replace:
                RemoveTabs(TabHost, args.OldStartingIndex, args.OldItems!.Count);
                InsertTabs(TabHost, args.NewStartingIndex, args.NewItems!);
                break;
            case NotifyCollectionChangedAction.Move:
                var movedTab = TabHost.Children[args.OldStartingIndex];
                TabHost.Children.RemoveAt(args.OldStartingIndex);
                TabHost.Children.Insert(args.NewStartingIndex, movedTab);
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
            AttachTab(tabItem);
            if (IsLoaded)
            {
                TabMotion.PrepareEntrance(tabItem);
            }

            tabHost.Children.Insert(index++, tabItem);
        }
    }

    private void RemoveTabs(HorizontalStackLayout tabHost, int startIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (startIndex < tabHost.Children.Count && tabHost.Children[startIndex] is TabItem tab)
            {
                DetachTab(tab);
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
        if (SelectedItem is null)
            return;

        foreach (var child in TabHost.Children.OfType<TabItem>())
        {
            if (Equals(child.BindingContext, SelectedItem))
            {
                await Task.Yield();
                var targetX = child.X;
                var tabWidth = child.Width;
                var viewportWidth = TabScrollView.Width;
                var scrollX = TabScrollView.ScrollX;

                if (targetX < scrollX)
                {
                    await TabScrollView.ScrollToAsync(targetX, 0, true);
                }
                else if (targetX + tabWidth > scrollX + viewportWidth)
                {
                    await TabScrollView.ScrollToAsync(targetX + tabWidth - viewportWidth, 0, true);
                }
                break;
            }
        }
    }

    private void RebuildTabs()
    {
        foreach (var existingTab in TabHost.Children.OfType<TabItem>().ToArray())
        {
            DetachTab(existingTab);
        }

        TabHost.Children.Clear();
        foreach (var item in _items)
        {
            var tabItem = CreateTabItem();
            ConfigureTabItem(tabItem, item);
            AttachTab(tabItem);
            TabHost.Children.Add(tabItem);
        }

        UpdateTabSelectionStates();
        UpdateVisualStates();
    }

    private void UpdateTabSelectionStates()
    {
        foreach (var tabItem in TabHost.Children.OfType<TabItem>())
        {
            tabItem.IsSelected = Equals(tabItem.BindingContext, SelectedItem);
        }
    }

    private void UpdateVisualStates()
    {
        VisualStateManager.GoToState(TabViewChromeRoot, GetAddButtonStateName());
        VisualStateManager.GoToState(TabViewChromeRoot, GetScrollButtonsVisibilityStateName());
        VisualStateManager.GoToState(TabViewChromeRoot, GetScrollButtonsAvailabilityStateName());
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
        if (!CanShowScrollButtons())
            return ScrollButtonsInactiveStateName;

        bool canScrollLeft = TabScrollView.ScrollX > 0;
        bool canScrollRight = TabScrollView.ScrollX + TabScrollView.Width < TabHost.Width - 1;

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
        return ScrollButtonsVisibility
            && TabHost.Width > TabScrollView.Width + 1;
    }

    private void AttachTab(TabItem tabItem)
    {
        // The tab strip is one keyboard focus group: Tab lands on the selected tab, ← and → move.
        tabItem.FocusGroup = _tabFocusGroup ??= AccessibleItemFocus.GroupFor(this);
        tabItem.SelectionRequested += TabItem_SelectionRequested;
        tabItem.CloseRequested += TabItem_CloseRequested;
        tabItem.DuplicateRequested += TabItem_DuplicateRequested;
        tabItem.MoveRequested += TabItem_MoveRequested;
        TabMotion.Track(tabItem);
    }

    private void DetachTab(TabItem tabItem)
    {
        tabItem.SelectionRequested -= TabItem_SelectionRequested;
        tabItem.CloseRequested -= TabItem_CloseRequested;
        tabItem.DuplicateRequested -= TabItem_DuplicateRequested;
        tabItem.MoveRequested -= TabItem_MoveRequested;
        TabMotion.Untrack(tabItem);
    }

    // ← or → on a focused tab selects its neighbour, and keyboard focus follows.
    private void TabItem_MoveRequested(object? sender, AccessibleItemMoveEventArgs e)
    {
        if (sender is not TabItem tabItem)
        {
            return;
        }

        var tabs = TabHost.Children.OfType<TabItem>().ToList();
        var index = tabs.IndexOf(tabItem) + e.Delta;
        if (index < 0 || index >= tabs.Count)
        {
            return;
        }

        SelectedItem = tabs[index].BindingContext;
        AccessibleItemFocus.MoveTo(tabs[index]);
        e.Handled = true;
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

    private void TabItem_DuplicateRequested(object? sender, EventArgs e)
    {
        if (sender is TabItem tabItem)
        {
            DuplicateRequested?.Invoke(this, tabItem);
        }
    }

    private async void LeftScrollButton_Click(object? sender, EventArgs e)
    {
        await TabScrollView.ScrollToAsync(Math.Max(0, TabScrollView.ScrollX - WidthPerScroll), 0, true);
        UpdateVisualStates();
    }

    private async void RightScrollButton_Click(object? sender, EventArgs e)
    {
        var maxOffset = Math.Max(0, TabHost.Width - TabScrollView.Width);
        await TabScrollView.ScrollToAsync(Math.Min(maxOffset, TabScrollView.ScrollX + WidthPerScroll), 0, true);
        UpdateVisualStates();
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        AddButtonClick?.Invoke(this, e);
    }
}

/// <summary>
/// The tab strip's item transitions, as UWP's CustomTabView style declares them
/// (ReorderThemeTransition, ContentThemeTransition, EntranceThemeTransition without stagger): FLIP
/// for tabs whose place changes (each is drawn where it was and its offset animated away), a fade and
/// short slide for a new tab. Off when Reduce Motion is on.
/// </summary>
internal static class TabMotion
{
    private const uint RepositionMs = 300;
    private const uint EntranceMs = 500;
    private const uint EntranceFadeMs = 250;
    private const double EntranceOffset = 40;

    // UWP's decelerate curve, cubic-bezier(0.1, 0.9, 0.2, 1).
    private static readonly Easing Decelerate = new(t => Bezier(t, 0.1, 0.9, 0.2, 1.0));

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TabItem, StrongBox> Pending = new();

    private sealed class StrongBox
    {
        public double X;
    }

    private static bool IsReduced =>
#if MACCATALYST || IOS
        UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        false;
#endif

    public static void CapturePositions(Layout tabHost)
    {
        if (IsReduced)
        {
            return;
        }

        foreach (var tab in tabHost.Children.OfType<TabItem>().Where(tab => tab.Width > 0))
        {
            Pending.AddOrUpdate(tab, new StrongBox { X = tab.X });
        }

        // Only the layout that follows the change animates; a later one (a window resize) does not.
        tabHost.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
        {
            foreach (var tab in tabHost.Children.OfType<TabItem>())
            {
                Pending.Remove(tab);
            }
        });
    }

    public static void Track(TabItem tab) => tab.PropertyChanged += OnTabPropertyChanged;

    public static void Untrack(TabItem tab)
    {
        tab.PropertyChanged -= OnTabPropertyChanged;
        Pending.Remove(tab);
    }

    public static void PrepareEntrance(TabItem tab)
    {
        if (IsReduced)
        {
            return;
        }

        tab.Opacity = 0;
        tab.TranslationX = EntranceOffset;
        void OnSized(object? sender, EventArgs e)
        {
            tab.SizeChanged -= OnSized;
            tab.FadeTo(1, EntranceFadeMs, Easing.Linear);
            tab.TranslateTo(0, tab.TranslationY, EntranceMs, Decelerate);
        }

        tab.SizeChanged += OnSized;
    }

    private static void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VisualElement.X) || sender is not TabItem tab || !Pending.TryGetValue(tab, out var from))
        {
            return;
        }

        Pending.Remove(tab);
        var delta = from.X - tab.X;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        tab.CancelAnimations();
        tab.TranslationX += delta;
        tab.TranslateTo(0, tab.TranslationY, RepositionMs, Decelerate);
    }

    // y of a CSS cubic-bezier at x = t (Newton iterations on x, then y).
    private static double Bezier(double t, double x1, double y1, double x2, double y2)
    {
        static double Curve(double u, double a, double b) => 3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u;
        static double Slope(double u, double a, double b) => 3 * a * (1 - u) * (1 - u) + 6 * (b - a) * u * (1 - u) + 3 * (1 - b) * u * u;

        var u = t;
        for (var i = 0; i < 8; i++)
        {
            var slope = Slope(u, x1, x2);
            if (Math.Abs(slope) < 1e-6)
            {
                break;
            }

            u -= (Curve(u, x1, x2) - t) / slope;
            u = Math.Clamp(u, 0, 1);
        }

        return Curve(u, y1, y2);
    }
}
