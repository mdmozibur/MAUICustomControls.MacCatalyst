namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A replacement for UWP SemanticZoom.
/// Hosts both a ZoomedInView and ZoomedOutView in a Grid and toggles between
/// them with an animated transition. The transition is driven by the platform
/// handler on MacCatalyst (UIKit animations) and falls back to MAUI animations
/// on other platforms.
/// </summary>
public sealed class SemanticZoom : ContentView
{
    private readonly Grid _container = new();
    private bool _isZoomedInViewActive = true;
    private bool _isTransitioning;
    private GroupableItemsView? _wiredZoomedOutItemsView;
    private object? _pendingNavigationGroup;

    public static readonly BindableProperty ZoomedInViewProperty =
        BindableProperty.Create(nameof(ZoomedInView), typeof(View), typeof(SemanticZoom), null, propertyChanged: OnViewChanged);

    public static readonly BindableProperty ZoomedOutViewProperty =
        BindableProperty.Create(nameof(ZoomedOutView), typeof(View), typeof(SemanticZoom), null, propertyChanged: OnViewChanged);

    public static readonly BindableProperty IsZoomedInViewHeaderStickyProperty =
        BindableProperty.Create(nameof(IsZoomedInViewHeaderSticky), typeof(bool), typeof(SemanticZoom), false);

    public View? ZoomedInView
    {
        get => (View?)GetValue(ZoomedInViewProperty);
        set => SetValue(ZoomedInViewProperty, value);
    }

    public View? ZoomedOutView
    {
        get => (View?)GetValue(ZoomedOutViewProperty);
        set => SetValue(ZoomedOutViewProperty, value);
    }

    /// <summary>
    /// Gets or sets whether grouped headers in the zoomed-in CollectionView
    /// remain visible while their group is scrolled. Default is false.
    /// </summary>
    public bool IsZoomedInViewHeaderSticky
    {
        get => (bool)GetValue(IsZoomedInViewHeaderStickyProperty);
        set => SetValue(IsZoomedInViewHeaderStickyProperty, value);
    }

    public bool IsZoomedInViewActive
    {
        get => _isZoomedInViewActive;
        set
        {
            if (_isZoomedInViewActive == value || _isTransitioning)
                return;

            var args = new SemanticZoomViewChangedEventArgs(_isZoomedInViewActive, _pendingNavigationGroup);
            _isZoomedInViewActive = value;
            ViewChangeStarted?.Invoke(this, args);
            PerformTransition(args.IsSourceZoomedInView);
        }
    }

    public event EventHandler<SemanticZoomViewChangedEventArgs>? ViewChangeStarted;

    public event EventHandler<SemanticZoomViewChangedEventArgs>? ViewChangeCompleted;

    /// <summary>
    /// Set by the platform handler to provide native animation.
    /// Parameters: (View incoming, View outgoing, bool zoomingOut, Action onComplete)
    /// </summary>
    internal Action<View, View, bool, Action>? PlatformAnimateTransition { get; set; }

    public SemanticZoom()
    {
        Content = _container;

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += (_, _) => ToggleActiveView();
        GestureRecognizers.Add(doubleTap);
    }

    public void ToggleActiveView()
    {
        IsZoomedInViewActive = !IsZoomedInViewActive;
    }

    /// <summary>
    /// Navigates to the given group: switches to the zoomed-in view (if needed)
    /// and scrolls its GroupableItemsView so the group's first item is at the top.
    /// This mirrors UWP SemanticZoom group navigation.
    /// </summary>
    public void NavigateToGroup(object group)
    {
        _pendingNavigationGroup = group;

        if (!_isZoomedInViewActive)
        {
            IsZoomedInViewActive = true;
            // PerformTransition consumes _pendingNavigationGroup; if the
            // transition was blocked (e.g. mid-transition) just clear it.
            if (!_isZoomedInViewActive)
                _pendingNavigationGroup = null;
        }
        else
        {
            ScrollZoomedInViewToPendingGroup();
        }
    }

    private void OnZoomedOutSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var group = e.CurrentSelection.FirstOrDefault();
        if (group == null)
            return;

        // Clear the selection so the same group can be tapped again later.
        if (sender is SelectableItemsView selectable)
            Dispatcher.Dispatch(() => selectable.SelectedItem = null);

        NavigateToGroup(group);
    }

    private void WireZoomedOutNavigation()
    {
        var itemsView = FindGroupableItemsView(ZoomedOutView);
        if (ReferenceEquals(itemsView, _wiredZoomedOutItemsView))
            return;

        if (_wiredZoomedOutItemsView != null)
            _wiredZoomedOutItemsView.SelectionChanged -= OnZoomedOutSelectionChanged;

        _wiredZoomedOutItemsView = itemsView;

        if (itemsView == null)
            return;

        // SemanticZoom owns group-click navigation, so make sure taps are
        // reported through selection (mirrors UWP's IsItemClickEnabled).
        if (itemsView.SelectionMode == SelectionMode.None)
            itemsView.SelectionMode = SelectionMode.Single;

        itemsView.SelectionChanged += OnZoomedOutSelectionChanged;
    }

    private void ScrollZoomedInViewToPendingGroup()
    {
        var group = _pendingNavigationGroup;
        _pendingNavigationGroup = null;
        if (group == null)
            return;

        var itemsView = FindGroupableItemsView(ZoomedInView);
        if (itemsView?.ItemsSource == null)
            return;

        var groupIndex = IndexOfGroup(itemsView.ItemsSource, group);
        if (groupIndex < 0)
            return;

        // Dispatch so the (possibly just-made-visible) view has a chance to lay out.
        Dispatcher.Dispatch(() =>
            itemsView.ScrollTo(index: 0, groupIndex: groupIndex, position: ScrollToPosition.Start, animate: false));
    }

    private static int IndexOfGroup(System.Collections.IEnumerable itemsSource, object group)
    {
        var target = UnwrapGroup(group);
        var index = 0;
        foreach (var item in itemsSource)
        {
            var candidate = UnwrapGroup(item);
            if (ReferenceEquals(candidate, target) || Equals(candidate, target))
                return index;
            index++;
        }
        return -1;
    }

    private static object? UnwrapGroup(object? item) =>
        item is CollectionViewGroup cvg ? cvg.Group : item;

    private static GroupableItemsView? FindGroupableItemsView(Element? root)
    {
        if (root == null)
            return null;

        if (root is GroupableItemsView itemsView)
            return itemsView;

        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren())
        {
            if (child is Element element && FindGroupableItemsView(element) is { } found)
                return found;
        }

        return null;
    }

    private void PerformTransition(bool zoomingOut)
    {
        var incoming = zoomingOut ? ZoomedOutView : ZoomedInView;
        var outgoing = zoomingOut ? ZoomedInView : ZoomedOutView;

        if (incoming == null)
        {
            if (outgoing != null)
                outgoing.IsVisible = false;
            return;
        }

        if (outgoing == null)
        {
            incoming.IsVisible = true;
            incoming.Opacity = 1;
            return;
        }

        _isTransitioning = true;

        // Prepare incoming view — invisible but in the tree
        incoming.IsVisible = true;
        incoming.Opacity = 0;

        // If this transition was triggered by group navigation, position the
        // zoomed-in list on the target group while it is still fading in.
        if (!zoomingOut)
            ScrollZoomedInViewToPendingGroup();

        if (PlatformAnimateTransition != null)
        {
            PlatformAnimateTransition(incoming, outgoing, zoomingOut, () =>
            {
                outgoing.IsVisible = false;
                _isTransitioning = false;
                ViewChangeCompleted?.Invoke(this, new SemanticZoomViewChangedEventArgs(zoomingOut));
            });
        }
        else
        {
            // Fallback: simple MAUI cross-fade
            FallbackAnimate(incoming, outgoing, zoomingOut);
        }
    }

    private async void FallbackAnimate(View incoming, View outgoing, bool zoomingOut)
    {
        const uint duration = 280;

        var scaleFrom = zoomingOut ? 1.1 : 0.9;
        incoming.Scale = scaleFrom;

        await Task.WhenAll(
            outgoing.FadeTo(0, duration, Easing.CubicIn),
            incoming.FadeTo(1, duration, Easing.CubicOut),
            incoming.ScaleTo(1, duration, Easing.CubicOut),
            outgoing.ScaleTo(zoomingOut ? 0.9 : 1.1, duration, Easing.CubicIn));

        outgoing.IsVisible = false;
        outgoing.Scale = 1;
        outgoing.Opacity = 1;
        _isTransitioning = false;
        ViewChangeCompleted?.Invoke(this, new SemanticZoomViewChangedEventArgs(zoomingOut));
    }

    private void RebuildContainer()
    {
        _container.Children.Clear();

        if (ZoomedInView != null)
        {
            ZoomedInView.IsVisible = _isZoomedInViewActive;
            ZoomedInView.Opacity = _isZoomedInViewActive ? 1 : 0;
            _container.Children.Add(ZoomedInView);
        }

        if (ZoomedOutView != null)
        {
            ZoomedOutView.IsVisible = !_isZoomedInViewActive;
            ZoomedOutView.Opacity = !_isZoomedInViewActive ? 1 : 0;
            _container.Children.Add(ZoomedOutView);
        }

        WireZoomedOutNavigation();
    }

    private static void OnViewChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is SemanticZoom semanticZoom)
        {
            semanticZoom.RebuildContainer();
        }
    }
}

public sealed class SemanticZoomViewChangedEventArgs : EventArgs
{
    public SemanticZoomViewChangedEventArgs(bool isSourceZoomedInView, object? sourceItem = null)
    {
        IsSourceZoomedInView = isSourceZoomedInView;
        SourceItem = sourceItem;
    }

    /// <summary>
    /// True when transitioning FROM zoomed-in TO zoomed-out.
    /// False when transitioning FROM zoomed-out TO zoomed-in.
    /// </summary>
    public bool IsSourceZoomedInView { get; }

    /// <summary>
    /// The group item that triggered the view change (when navigating from the
    /// zoomed-out view by clicking a group), otherwise null.
    /// </summary>
    public object? SourceItem { get; }
}
