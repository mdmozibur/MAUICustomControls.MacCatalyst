namespace MAUICustomControls.MacCatalyst.Controls;

public enum AnimatedVisualStretch
{
    None,
    Fill,
    Uniform,
    UniformToFill,
}

internal interface IAnimatedVisualPlayerController
{
    Task PlayAsync(double fromProgress, double toProgress, bool looped);
    void Stop();
    void Pause();
    void Resume();
    void SetProgress(double progress);
}

/// <summary>
/// MAUI-compatible playback surface modeled after WinUI AnimatedVisualPlayer.
/// The Mac Catalyst handler renders Lottie JSON through Airbnb Lottie and Core Animation.
/// </summary>
public sealed class AnimatedVisualPlayer : View
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(string), typeof(AnimatedVisualPlayer), string.Empty);

    public static readonly BindableProperty AutoPlayProperty = BindableProperty.Create(
        nameof(AutoPlay), typeof(bool), typeof(AnimatedVisualPlayer), false);

    public static readonly BindableProperty StretchProperty = BindableProperty.Create(
        nameof(Stretch), typeof(AnimatedVisualStretch), typeof(AnimatedVisualPlayer), AnimatedVisualStretch.Uniform);

    public static readonly BindableProperty PlaybackRateProperty = BindableProperty.Create(
        nameof(PlaybackRate),
        typeof(double),
        typeof(AnimatedVisualPlayer),
        1d,
        coerceValue: static (_, value) => Math.Max(0d, (double)value));

    public static readonly BindableProperty IsAnimatedVisualLoadedProperty = BindableProperty.Create(
        nameof(IsAnimatedVisualLoaded),
        typeof(bool),
        typeof(AnimatedVisualPlayer),
        false,
        BindingMode.OneWayToSource);

    public static readonly BindableProperty IsPlayingProperty = BindableProperty.Create(
        nameof(IsPlaying), typeof(bool), typeof(AnimatedVisualPlayer), false, BindingMode.OneWayToSource);

    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration), typeof(TimeSpan), typeof(AnimatedVisualPlayer), TimeSpan.Zero, BindingMode.OneWayToSource);

    private readonly Dictionary<long, (BindableProperty Property, Action<BindableObject, BindableProperty> Callback)> _callbacks = [];
    private long _nextCallbackToken;

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool AutoPlay
    {
        get => (bool)GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    public AnimatedVisualStretch Stretch
    {
        get => (AnimatedVisualStretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public double PlaybackRate
    {
        get => (double)GetValue(PlaybackRateProperty);
        set => SetValue(PlaybackRateProperty, value);
    }

    public bool IsAnimatedVisualLoaded => (bool)GetValue(IsAnimatedVisualLoadedProperty);
    public bool IsPlaying => (bool)GetValue(IsPlayingProperty);
    public TimeSpan Duration => (TimeSpan)GetValue(DurationProperty);

    public Task PlayAsync(double fromProgress, double toProgress, bool looped)
    {
        return Controller?.PlayAsync(
            Math.Clamp(fromProgress, 0d, 1d),
            Math.Clamp(toProgress, 0d, 1d),
            looped) ?? Task.CompletedTask;
    }

    public void Stop() => Controller?.Stop();
    public void Pause() => Controller?.Pause();
    public void Resume() => Controller?.Resume();
    public void SetProgress(double progress) => Controller?.SetProgress(Math.Clamp(progress, 0d, 1d));

    public long RegisterPropertyChangedCallback(
        BindableProperty property,
        Action<BindableObject, BindableProperty> callback)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(callback);

        var token = Interlocked.Increment(ref _nextCallbackToken);
        _callbacks[token] = (property, callback);
        return token;
    }

    public void UnregisterPropertyChangedCallback(BindableProperty property, long token)
    {
        if (_callbacks.TryGetValue(token, out var registration) && registration.Property == property)
        {
            _callbacks.Remove(token);
        }
    }

    internal void SetIsAnimatedVisualLoaded(bool value) => SetValue(IsAnimatedVisualLoadedProperty, value);
    internal void SetIsPlaying(bool value) => SetValue(IsPlayingProperty, value);
    internal void SetDuration(TimeSpan value) => SetValue(DurationProperty, value);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (string.IsNullOrEmpty(propertyName) || _callbacks.Count == 0)
        {
            return;
        }

        foreach (var registration in _callbacks.Values.ToArray())
        {
            if (string.Equals(registration.Property.PropertyName, propertyName, StringComparison.Ordinal))
            {
                registration.Callback(this, registration.Property);
            }
        }
    }

    private IAnimatedVisualPlayerController? Controller => Handler as IAnimatedVisualPlayerController;
}
