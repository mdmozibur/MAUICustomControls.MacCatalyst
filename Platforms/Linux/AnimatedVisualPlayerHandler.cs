namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using MAUICustomControls.MacCatalyst.Controls;
using SkiaSharp;
using SkiaSharp.Skottie;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;

/// <summary>
/// Plays Lottie JSON with Skia's Skottie (the engine behind Avalonia's own renderer): each frame
/// the animation is drawn straight onto the window's Skia canvas.
/// </summary>
public sealed class LottieView : Avalonia.Controls.Control
{
    private readonly DispatcherTimer _timer;
    private Animation? _animation;
    private double _progress;
    private double _from;
    private double _to = 1;
    private bool _looped;
    private DateTime _lastTick;

    public LottieView()
    {
        ClipToBounds = true;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
    }

    /// <summary>Raised when a non-looping play reaches its end.</summary>
    public event EventHandler? Completed;

    public AnimatedVisualStretch Stretch { get; set { field = value; InvalidateVisual(); } } = AnimatedVisualStretch.Uniform;

    public double PlaybackRate { get; set; } = 1;

    public bool IsPlaying => _timer.IsEnabled;

    public TimeSpan Duration => _animation is null ? TimeSpan.Zero : _animation.Duration;

    public bool Load(string? path)
    {
        Stop();
        _animation?.Dispose();
        _animation = null;
        if (path is null || !File.Exists(path))
        {
            InvalidateVisual();
            return false;
        }

        using var stream = File.OpenRead(path);
        _animation = Animation.Create(stream);
        _progress = 0;
        InvalidateVisual();
        return _animation is not null && _animation.Duration > TimeSpan.Zero;
    }

    public void Play(double from, double to, bool looped)
    {
        _from = from;
        _to = to;
        _looped = looped;
        _progress = from;
        _lastTick = DateTime.UtcNow;
        _timer.Start();
        InvalidateVisual();
    }

    public void Stop()
    {
        _timer.Stop();
        _progress = _from;
        InvalidateVisual();
    }

    public void Pause() => _timer.Stop();

    public void Resume()
    {
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    public void SetProgress(double progress)
    {
        _progress = progress;
        InvalidateVisual();
    }

    private void Tick()
    {
        if (_animation is null)
        {
            _timer.Stop();
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        var seconds = Math.Max(_animation.Duration.TotalSeconds, 0.001);
        _progress += elapsed * PlaybackRate / seconds * Math.Sign(_to - _from == 0 ? 1 : _to - _from);

        var reachedEnd = _to >= _from ? _progress >= _to : _progress <= _to;
        if (reachedEnd)
        {
            if (_looped)
            {
                _progress = _from;
            }
            else
            {
                _progress = _to;
                _timer.Stop();
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (_animation is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var size = _animation.Size;
        var target = Fit(new SKSize(size.Width, size.Height), new SKSize((float)Bounds.Width, (float)Bounds.Height), Stretch);
        context.Custom(new LottieDrawOperation(new Rect(Bounds.Size), _animation, Math.Clamp(_progress, 0, 1), target));
    }

    private static SKRect Fit(SKSize content, SKSize box, AnimatedVisualStretch stretch)
    {
        if (content.Width <= 0 || content.Height <= 0 || stretch == AnimatedVisualStretch.Fill)
        {
            return new SKRect(0, 0, box.Width, box.Height);
        }

        var scaleX = box.Width / content.Width;
        var scaleY = box.Height / content.Height;
        var scale = stretch switch
        {
            AnimatedVisualStretch.None => 1f,
            AnimatedVisualStretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY),
        };
        var width = content.Width * scale;
        var height = content.Height * scale;
        var left = (box.Width - width) / 2;
        var top = (box.Height - height) / 2;
        return new SKRect(left, top, left + width, top + height);
    }

    private sealed class LottieDrawOperation(Rect bounds, Animation animation, double progress, SKRect target) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } leaseFeature)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            animation.SeekFrameTime(progress * animation.Duration.TotalSeconds);
            animation.Render(canvas, target);
            canvas.Restore();
        }
    }
}

/// <summary>WinUI's AnimatedVisualPlayer on Linux: Lottie JSON from the app's Assets, played by Skottie.</summary>
public sealed class AnimatedVisualPlayerHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<AnimatedVisualPlayer, LottieView>, IAnimatedVisualPlayerController
{
    public static readonly PropertyMapper<AnimatedVisualPlayer, AnimatedVisualPlayerHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(AnimatedVisualPlayer.Source)] = (handler, _) => handler.LoadSource(),
        [nameof(AnimatedVisualPlayer.AutoPlay)] = MapAutoPlay,
        [nameof(AnimatedVisualPlayer.Stretch)] = (handler, player) => handler.PlatformView.Stretch = player.Stretch,
        [nameof(AnimatedVisualPlayer.PlaybackRate)] = (handler, player) => handler.PlatformView.PlaybackRate = player.PlaybackRate,
    };

    private TaskCompletionSource? _playCompletion;

    public AnimatedVisualPlayerHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override LottieView CreatePlatformView() => new();

    protected override void ConnectHandler(LottieView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Completed += OnCompleted;
    }

    protected override void DisconnectHandler(LottieView platformView)
    {
        platformView.Completed -= OnCompleted;
        Stop();
        base.DisconnectHandler(platformView);
    }

    public Task PlayAsync(double fromProgress, double toProgress, bool looped)
    {
        _playCompletion?.TrySetResult();
        _playCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PlatformView.Play(fromProgress, toProgress, looped);
        VirtualView?.SetIsPlaying(true);
        return _playCompletion.Task;
    }

    public void Stop()
    {
        PlatformView?.Stop();
        VirtualView?.SetIsPlaying(false);
        _playCompletion?.TrySetResult();
        _playCompletion = null;
    }

    public void Pause()
    {
        PlatformView.Pause();
        VirtualView?.SetIsPlaying(false);
    }

    public void Resume()
    {
        PlatformView.Resume();
        VirtualView?.SetIsPlaying(true);
    }

    public void SetProgress(double progress) => PlatformView.SetProgress(progress);

    private void OnCompleted(object? sender, EventArgs e)
    {
        VirtualView?.SetIsPlaying(false);
        _playCompletion?.TrySetResult();
        _playCompletion = null;
    }

    private static void MapAutoPlay(AnimatedVisualPlayerHandler handler, AnimatedVisualPlayer player)
    {
        if (player.AutoPlay && player.IsAnimatedVisualLoaded && !player.IsPlaying)
        {
            _ = handler.PlayAsync(0d, 1d, false);
        }
    }

    private void LoadSource()
    {
        if (VirtualView is not { } player)
        {
            return;
        }

        Stop();
        var loaded = PlatformView.Load(ResolveSourcePath(player.Source));
        player.SetDuration(loaded ? PlatformView.Duration : TimeSpan.Zero);
        player.SetIsAnimatedVisualLoaded(loaded);
        if (loaded && player.AutoPlay && !player.IsPlaying)
        {
            _ = PlayAsync(0d, 1d, false);
        }
    }

    // ms-appx:///Assets/x.json is the app's Assets folder, copied next to the executable.
    private static string? ResolveSourcePath(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (Path.IsPathRooted(source) && File.Exists(source))
        {
            return source;
        }

        const string appPackagePrefix = "ms-appx:///";
        var logicalPath = source.StartsWith(appPackagePrefix, StringComparison.OrdinalIgnoreCase)
            ? source[appPackagePrefix.Length..]
            : source.TrimStart('/');
        var path = Path.Combine(AppContext.BaseDirectory, logicalPath.Replace('\\', '/'));
        return File.Exists(path) ? path : null;
    }
}
