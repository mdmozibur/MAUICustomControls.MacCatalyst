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

    public static readonly BindableProperty ZoomedInViewProperty =
        BindableProperty.Create(nameof(ZoomedInView), typeof(View), typeof(SemanticZoom), null, propertyChanged: OnViewChanged);

    public static readonly BindableProperty ZoomedOutViewProperty =
        BindableProperty.Create(nameof(ZoomedOutView), typeof(View), typeof(SemanticZoom), null, propertyChanged: OnViewChanged);

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

    public bool IsZoomedInViewActive
    {
        get => _isZoomedInViewActive;
        set
        {
            if (_isZoomedInViewActive == value || _isTransitioning)
                return;

            var args = new SemanticZoomViewChangedEventArgs(_isZoomedInViewActive);
            _isZoomedInViewActive = value;
            ViewChangeStarted?.Invoke(this, args);
            PerformTransition(args.IsSourceZoomedInView);
        }
    }

    public event EventHandler<SemanticZoomViewChangedEventArgs>? ViewChangeStarted;

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

        if (PlatformAnimateTransition != null)
        {
            PlatformAnimateTransition(incoming, outgoing, zoomingOut, () =>
            {
                outgoing.IsVisible = false;
                _isTransitioning = false;
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
    public SemanticZoomViewChangedEventArgs(bool isSourceZoomedInView)
    {
        IsSourceZoomedInView = isSourceZoomedInView;
    }

    /// <summary>
    /// True when transitioning FROM zoomed-in TO zoomed-out.
    /// False when transitioning FROM zoomed-out TO zoomed-in.
    /// </summary>
    public bool IsSourceZoomedInView { get; }
}
