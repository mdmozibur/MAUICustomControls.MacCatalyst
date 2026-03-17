namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class RepeatButton : Button
{
    public static readonly BindableProperty DelayProperty =
        BindableProperty.Create(nameof(Delay), typeof(int), typeof(RepeatButton), 400);

    public static readonly BindableProperty IntervalProperty =
        BindableProperty.Create(nameof(Interval), typeof(int), typeof(RepeatButton), 100);

    private CancellationTokenSource? _repeatCancellation;
    private bool _suppressNextBaseClick;

    public new event EventHandler? Clicked;

    public int Delay
    {
        get => (int)GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    public int Interval
    {
        get => (int)GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    public RepeatButton()
    {
        base.Clicked += OnBaseClicked;
        Pressed += OnPressed;
        Released += OnReleased;
        HandlerChanging += OnHandlerChanging;
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        StopRepeating();
        _suppressNextBaseClick = true;
        Clicked?.Invoke(this, EventArgs.Empty);

        _repeatCancellation = new CancellationTokenSource();
        _ = RepeatAsync(_repeatCancellation.Token);
    }

    private void OnReleased(object? sender, EventArgs e)
    {
        StopRepeating();
    }

    private void OnBaseClicked(object? sender, EventArgs e)
    {
        if (_suppressNextBaseClick)
        {
            _suppressNextBaseClick = false;
            return;
        }

        Clicked?.Invoke(this, e);
    }

    private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e)
    {
        if (e.NewHandler == null)
        {
            StopRepeating();
        }
    }

    private async Task RepeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Math.Max(0, Delay), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
                await Task.Delay(Math.Max(1, Interval), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StopRepeating()
    {
        if (_repeatCancellation == null)
        {
            return;
        }

        _repeatCancellation.Cancel();
        _repeatCancellation.Dispose();
        _repeatCancellation = null;
    }
}