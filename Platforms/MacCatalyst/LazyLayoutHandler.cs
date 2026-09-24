using System.Diagnostics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using ILayout = Microsoft.Maui.ILayout;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// MAUI's LayoutHandler, except that a child which is hidden (IsVisible false) when it joins the
/// layout gets no platform view straight away.
///
/// MAUI creates a UIView for every element up front, visible or not, at roughly half a millisecond
/// each. UWP-style panes keep their tool panels collapsed until needed: the SmartCAD left pane holds
/// about 2,300 elements, nearly all of them in hidden panels, and inserting it cost about two seconds
/// on the main thread. Here those children are realized later by <see cref="DeferredRealization"/>,
/// bottom-up in short idle slices, or at once when one is about to be shown.
///
/// Realizing them soon rather than on first show keeps UWP's Loaded semantics: a collapsed UWP
/// element is loaded with its page, and views such as BindingView subscribe to their view model in
/// Loaded before the pane shows them. A child about to be shown is realized on PropertyChanging,
/// while it is still hidden, so its Visibility mapping (and any implicit show animation) runs on a
/// platform view that is already in the window, exactly as with MAUI's own handler.
/// </summary>
public class LazyLayoutHandler : ViewHandler<ILayout, LayoutView>, ILayoutHandler
{
    private readonly HashSet<BindableObject> _deferred = new(ReferenceEqualityComparer.Instance);

    public LazyLayoutHandler()
        : base(LayoutHandler.Mapper, LayoutHandler.CommandMapper)
    {
    }

    public LazyLayoutHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null)
        : base(mapper ?? LayoutHandler.Mapper, commandMapper ?? LayoutHandler.CommandMapper)
    {
    }

    ILayout ILayoutHandler.VirtualView => VirtualView;

    LayoutView ILayoutHandler.PlatformView => PlatformView;

    protected override LayoutView CreatePlatformView()
    {
        if (VirtualView is null)
        {
            throw new InvalidOperationException("VirtualView must be set to create a LayoutView");
        }

        return new LayoutView { CrossPlatformLayout = VirtualView };
    }

    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);

        PlatformView.View = view;
        PlatformView.CrossPlatformLayout = VirtualView;
        StopWatchingAll();
        PlatformView.ClearSubviews();

        foreach (var child in OrderByZIndex(VirtualView))
        {
            if (ShouldDefer(child))
            {
                Defer(child);
            }
            else
            {
                PlatformView.AddSubview(child.ToPlatform(MauiContext!));
            }
        }

        InvalidateAncestorsMeasures(PlatformView);
    }

    public void Add(IView child) => Attach(child);

    // The index is the child's place among all children; a platform index is derived from the
    // realized ones when it is inserted.
    public void Insert(int index, IView child) => Attach(child);

    public void Remove(IView child)
    {
        StopWatching(child);
        if (PlatformOf(child) is { } platformView && ReferenceEquals(platformView.Superview, PlatformView))
        {
            platformView.RemoveFromSuperview();
        }

        InvalidateAncestorsMeasures(PlatformView);
    }

    public void Clear()
    {
        StopWatchingAll();
        PlatformView.ClearSubviews();
        InvalidateAncestorsMeasures(PlatformView);
    }

    // The child at index was replaced; MAUI does not say by what it replaced, so drop the platform
    // views of children the layout no longer has, then attach the new one.
    public void Update(int index, IView child)
    {
        var current = new HashSet<UIView>(ReferenceEqualityComparer.Instance);
        foreach (var existing in VirtualView)
        {
            if (PlatformOf(existing) is { } platformView)
            {
                current.Add(platformView);
            }
        }

        foreach (var subview in PlatformView.Subviews)
        {
            if (!current.Contains(subview))
            {
                subview.RemoveFromSuperview();
            }
        }

        foreach (var watched in _deferred.ToList())
        {
            if (watched is not IView watchedView || !VirtualView.Contains(watchedView))
            {
                StopWatching(watched as IView);
            }
        }

        Attach(child);
        PlatformView.SetNeedsLayout();
    }

    public void UpdateZIndex(IView child)
    {
        if (PlatformOf(child) is not { } platformView || !ReferenceEquals(platformView.Superview, PlatformView))
        {
            return;
        }

        var target = RealizedIndexOf(child);
        var current = Array.IndexOf(PlatformView.Subviews, platformView);
        if (current != target)
        {
            platformView.RemoveFromSuperview();
            PlatformView.InsertSubview(platformView, Math.Min(target, PlatformView.Subviews.Length));
        }
    }

    protected override void DisconnectHandler(LayoutView platformView)
    {
        StopWatchingAll();
        base.DisconnectHandler(platformView);
        platformView.ClearSubviews();
    }

    /// <summary>Whether <paramref name="child"/> is waiting for its platform view.</summary>
    internal bool IsDeferred(IView child) => child is BindableObject bindable && _deferred.Contains(bindable);

    /// <summary>
    /// Gives a deferred child its platform view and puts it in place. Its descendants may already
    /// have theirs (DeferredRealization builds them bottom-up), which ToPlatform then reuses.
    /// </summary>
    internal void Realize(IView child)
    {
        if (VirtualView is null || MauiContext is null || !VirtualView.Contains(child))
        {
            StopWatching(child);
            return;
        }

        StopWatching(child);
        InsertPlatformView(child);

        // A child still collapsed takes no space, so the layout's size stands; invalidating it would
        // re-measure the whole pane once per panel built in the background. A child realized because
        // it is about to show gets its measure invalidated by the visibility change itself.
        if (child.Visibility != Visibility.Collapsed)
        {
            InvalidateAncestorsMeasures(PlatformView);
        }
    }

    private void Attach(IView child)
    {
        if (ShouldDefer(child))
        {
            Defer(child);
            return;
        }

        StopWatching(child);
        InsertPlatformView(child);
        InvalidateAncestorsMeasures(PlatformView);
    }

    private void InsertPlatformView(IView child)
    {
        var platformView = child.ToPlatform(MauiContext!);
        if (!ReferenceEquals(platformView.Superview, PlatformView))
        {
            platformView.RemoveFromSuperview();
            PlatformView.InsertSubview(platformView, Math.Min(RealizedIndexOf(child), PlatformView.Subviews.Length));
        }

        if (child.FlowDirection == FlowDirection.MatchParent)
        {
            platformView.UpdateFlowDirection(child);
        }
    }

    // Only a child that has no handler yet waits: one realized before (hidden again, or built by
    // DeferredRealization) costs nothing to put back.
    private static bool ShouldDefer(IView child) =>
        child is VisualElement { IsVisible: false, Handler: null };

    private void Defer(IView child)
    {
        if (child is not BindableObject bindable || !_deferred.Add(bindable))
        {
            return;
        }

        bindable.PropertyChanging += OnDeferredChildPropertyChanging;
        DeferredRealization.Enqueue(this, child);
    }

    private void OnDeferredChildPropertyChanging(object? sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
    {
        if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName && sender is IView child)
        {
            Realize(child);
        }
    }

    private void StopWatching(IView? child)
    {
        if (child is BindableObject bindable && _deferred.Remove(bindable))
        {
            bindable.PropertyChanging -= OnDeferredChildPropertyChanging;
        }
    }

    private void StopWatchingAll()
    {
        foreach (var bindable in _deferred)
        {
            bindable.PropertyChanging -= OnDeferredChildPropertyChanging;
        }

        _deferred.Clear();
    }

    // The child's position in z order among the children that have a platform view here.
    private int RealizedIndexOf(IView child)
    {
        var index = 0;
        foreach (var candidate in OrderByZIndex(VirtualView))
        {
            if (ReferenceEquals(candidate, child))
            {
                break;
            }

            if (PlatformOf(candidate) is { } platformView && ReferenceEquals(platformView.Superview, PlatformView))
            {
                index++;
            }
        }

        return index;
    }

    // MAUI's own OrderByZIndex is internal: a stable sort by ZIndex, children order breaking ties.
    private static IEnumerable<IView> OrderByZIndex(ILayout layout) => layout.OrderBy(view => view.ZIndex);

    private static UIView? PlatformOf(IView? view) =>
        view?.Handler is IPlatformViewHandler handler ? handler.ContainerView ?? handler.PlatformView : null;

    // MAUI's own InvalidateAncestorsMeasures is internal; this is the same walk.
    private static void InvalidateAncestorsMeasures(UIView child)
    {
        var controller = child as IPlatformMeasureInvalidationController;
        while (true)
        {
            if (controller is not null && child.Window is null)
            {
                controller.InvalidateAncestorsMeasuresWhenMovedToWindow();
                return;
            }

            if (child.Superview is not { } superview)
            {
                return;
            }

            var propagate = true;
            var superviewController = superview as IPlatformMeasureInvalidationController;
            if (superviewController is not null)
            {
                propagate = superviewController.InvalidateMeasure(isPropagating: true);
            }
            else
            {
                superview.SetNeedsLayout();
            }

            if (!propagate)
            {
                return;
            }

            child = superview;
            controller = superviewController;
        }
    }
}

/// <summary>
/// Builds platform views on the main thread in slices of a few milliseconds, yielding to the run
/// loop between slices so input and drawing carry on. Each element's children get their platform
/// views before the element itself, so every step is one element's handler, never a whole subtree.
/// </summary>
public static class DeferredRealization
{
    private const double SliceMilliseconds = 6;
    private static readonly TimeSpan StartDelay = TimeSpan.FromMilliseconds(250);

    // Building a large hidden panel can hold the main thread for a few hundred milliseconds (its
    // layout, its Loaded handlers), which would stutter an animation in progress, such as the pane
    // sliding in as it appears. The work waits while MAUI animations run, but no longer than this.
    private static readonly TimeSpan AnimationPollDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaxAnimationWait = TimeSpan.FromSeconds(1.5);
    private static WeakReference<Microsoft.Maui.Animations.IAnimationManager>? s_animationManager;
    private static DateTime? s_animationWaitStarted;
    private static readonly Queue<(WeakReference<LazyLayoutHandler> Owner, WeakReference<IView> Child)> Pending = new();
    private static bool s_scheduled;

    /// <summary>Queues a hidden child whose layout deferred its platform view.</summary>
    internal static void Enqueue(LazyLayoutHandler owner, IView child)
    {
        if (owner.MauiContext?.Services.GetService(typeof(Microsoft.Maui.Animations.IAnimationManager)) is Microsoft.Maui.Animations.IAnimationManager animations)
        {
            s_animationManager = new WeakReference<Microsoft.Maui.Animations.IAnimationManager>(animations);
        }

        Pending.Enqueue((new WeakReference<LazyLayoutHandler>(owner), new WeakReference<IView>(child)));
        Schedule(StartDelay);
    }

    /// <summary>
    /// Builds the platform views of <paramref name="root"/> and its visible descendants in idle
    /// slices, before the view is put on screen, so attaching it later costs little. Hidden
    /// subtrees are left to their layouts, which queue them here once their parent is built.
    /// </summary>
    public static async Task PrebuildAsync(IView root, IMauiContext context, CancellationToken cancellationToken = default)
    {
        var order = new List<IView>();
        CollectPostOrder(root, order, includeHidden: false);
        var stopwatch = Stopwatch.StartNew();
        foreach (var view in order)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (view.Handler is null)
            {
                view.ToPlatform(context);
            }

            if (stopwatch.Elapsed.TotalMilliseconds >= SliceMilliseconds)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(true);
                stopwatch.Restart();
            }
        }
    }

    private static void Schedule(TimeSpan delay)
    {
        if (s_scheduled)
        {
            return;
        }

        if (Application.Current?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        s_scheduled = true;
        dispatcher.DispatchDelayed(delay, RunSlice);
    }

    // The deferred child currently being built, as a post-order list of what still needs a handler.
    private static (WeakReference<LazyLayoutHandler> Owner, IView Child, List<IView> Order, int Next)? s_current;

    private static void RunSlice()
    {
        s_scheduled = false;
        if (IsAnimating())
        {
            s_animationWaitStarted ??= DateTime.UtcNow;
            if (DateTime.UtcNow - s_animationWaitStarted < MaxAnimationWait)
            {
                Schedule(AnimationPollDelay);
                return;
            }
        }

        s_animationWaitStarted = null;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalMilliseconds < SliceMilliseconds)
        {
            if (s_current is not { } current)
            {
                if (!TryStartNext())
                {
                    return;
                }

                continue;
            }

            if (!current.Owner.TryGetTarget(out var owner) || !owner.IsDeferred(current.Child) || owner.MauiContext is not { } context)
            {
                s_current = null;
                continue;
            }

            if (current.Next < current.Order.Count)
            {
                var view = current.Order[current.Next];
                s_current = current with { Next = current.Next + 1 };
                if (view.Handler is null)
                {
                    try
                    {
                        view.ToPlatform(context);
                    }
                    catch (Exception ex)
                    {
                        // Left for the layout to build when it realizes the child, as MAUI would have.
                        Debug.WriteLine($"DeferredRealization: {view.GetType().Name} failed: {ex.Message}");
                    }
                }

                continue;
            }

            s_current = null;
            owner.Realize(current.Child);
        }

        Schedule(TimeSpan.FromMilliseconds(1));
    }

    private static bool IsAnimating() =>
        s_animationManager is not null &&
        s_animationManager.TryGetTarget(out var animations) &&
        animations.Ticker.IsRunning;

    private static bool TryStartNext()
    {
        while (Pending.Count > 0)
        {
            var (ownerReference, childReference) = Pending.Dequeue();
            if (!ownerReference.TryGetTarget(out var owner) ||
                !childReference.TryGetTarget(out var child) ||
                !owner.IsDeferred(child))
            {
                continue;
            }

            // The child itself is realized by its layout, which puts it in place; only its
            // descendants are built here.
            var order = new List<IView>();
            CollectPostOrder(child, order, includeHidden: true);
            order.Remove(child);
            s_current = (ownerReference, child, order, 0);
            return true;
        }

        return false;
    }

    private static void CollectPostOrder(IView view, List<IView> order, bool includeHidden)
    {
        if (!includeHidden && view is VisualElement { IsVisible: false })
        {
            return;
        }

        if (view is IVisualTreeElement element)
        {
            foreach (var child in element.GetVisualChildren())
            {
                if (child is IView childView)
                {
                    CollectPostOrder(childView, order, includeHidden);
                }
            }
        }

        order.Add(view);
    }
}
