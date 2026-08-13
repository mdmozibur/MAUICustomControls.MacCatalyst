using Foundation;
using Lottie.MacCatalyst.Binding;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class AnimatedVisualPlayerHandler : ViewHandler<AnimatedVisualPlayer, UIView>, IAnimatedVisualPlayerController
{
    private CompatibleAnimationView? _animationView;
    private TaskCompletionSource? _playCompletion;
    private double _lastToProgress = 1d;
    private bool _lastLooped;
    private long _playVersion;

    public static readonly IPropertyMapper<AnimatedVisualPlayer, AnimatedVisualPlayerHandler> Mapper =
        new PropertyMapper<AnimatedVisualPlayer, AnimatedVisualPlayerHandler>(ViewMapper)
        {
            [nameof(AnimatedVisualPlayer.Source)] = MapSource,
            [nameof(AnimatedVisualPlayer.AutoPlay)] = MapAutoPlay,
            [nameof(AnimatedVisualPlayer.Stretch)] = MapStretch,
            [nameof(AnimatedVisualPlayer.PlaybackRate)] = MapPlaybackRate,
        };

    public AnimatedVisualPlayerHandler() : base(Mapper)
    {
    }

    protected override UIView CreatePlatformView() => new()
    {
        BackgroundColor = UIColor.Clear,
        ClipsToBounds = true,
    };

    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        LoadSource();
    }

    protected override void DisconnectHandler(UIView platformView)
    {
        Stop();
        RemoveAnimationView();

        if (VirtualView is not null)
        {
            VirtualView.SetIsAnimatedVisualLoaded(false);
            VirtualView.SetDuration(TimeSpan.Zero);
        }

        base.DisconnectHandler(platformView);
    }

    public Task PlayAsync(double fromProgress, double toProgress, bool looped)
    {
        Stop();

        if (_animationView is null || VirtualView is null || !VirtualView.IsAnimatedVisualLoaded)
        {
            return Task.CompletedTask;
        }

        _lastToProgress = toProgress;
        _lastLooped = looped;
        _playCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartNativePlayback(fromProgress, toProgress, looped);
        return _playCompletion.Task;
    }

    public void Stop()
    {
        _playVersion++;
        _animationView?.Stop();
        VirtualView?.SetIsPlaying(false);
        _playCompletion?.TrySetResult();
        _playCompletion = null;
    }

    public void Pause()
    {
        _animationView?.Pause();
        VirtualView?.SetIsPlaying(false);
    }

    public void Resume()
    {
        if (_animationView is null || _playCompletion is null || VirtualView is null)
        {
            return;
        }

        StartNativePlayback((double)_animationView.CurrentProgress, _lastToProgress, _lastLooped);
    }

    public void SetProgress(double progress)
    {
        if (_animationView is not null)
        {
            _animationView.CurrentProgress = (nfloat)progress;
        }
    }

    private static void MapSource(AnimatedVisualPlayerHandler handler, AnimatedVisualPlayer control) => handler.LoadSource();

    private static void MapAutoPlay(AnimatedVisualPlayerHandler handler, AnimatedVisualPlayer control)
    {
        if (control.AutoPlay && control.IsAnimatedVisualLoaded && !control.IsPlaying)
        {
            _ = handler.PlayAsync(0d, 1d, false);
        }
    }

    private static void MapStretch(AnimatedVisualPlayerHandler handler, AnimatedVisualPlayer control)
    {
        if (handler._animationView is not null)
        {
            handler._animationView.ContentMode = ToContentMode(control.Stretch);
        }
    }

    private static void MapPlaybackRate(AnimatedVisualPlayerHandler handler, AnimatedVisualPlayer control)
    {
        if (handler._animationView is not null)
        {
            handler._animationView.AnimationSpeed = (nfloat)control.PlaybackRate;
        }
    }

    private void LoadSource()
    {
        if (PlatformView is null || VirtualView is null)
        {
            return;
        }

        Stop();
        RemoveAnimationView();
        VirtualView.SetIsAnimatedVisualLoaded(false);
        VirtualView.SetDuration(TimeSpan.Zero);

        var sourcePath = ResolveSourcePath(VirtualView.Source);
        if (sourcePath is null)
        {
            return;
        }

        using var data = NSData.FromFile(sourcePath);
        if (data is null || data.Length == 0)
        {
            return;
        }

        var animationView = new CompatibleAnimationView(data)
        {
            Frame = PlatformView.Bounds,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            BackgroundColor = UIColor.Clear,
            ContentMode = ToContentMode(VirtualView.Stretch),
            AnimationSpeed = (nfloat)VirtualView.PlaybackRate,
        };

        if (animationView.Duration <= 0)
        {
            animationView.Dispose();
            return;
        }

        _animationView = animationView;
        PlatformView.AddSubview(animationView);
        VirtualView.SetDuration(TimeSpan.FromSeconds((double)animationView.Duration));
        VirtualView.SetIsAnimatedVisualLoaded(true);

        // The loaded-property callback may already have started looping playback.
        if (VirtualView.AutoPlay && !VirtualView.IsPlaying)
        {
            _ = PlayAsync(0d, 1d, false);
        }
    }

    private void StartNativePlayback(double fromProgress, double toProgress, bool looped)
    {
        if (_animationView is null || VirtualView is null)
        {
            return;
        }

        var version = ++_playVersion;
        _animationView.LoopAnimationCount = looped ? -1 : 0;
        _animationView.AnimationSpeed = (nfloat)VirtualView.PlaybackRate;
        VirtualView.SetIsPlaying(true);
        _animationView.Play((nfloat)fromProgress, (nfloat)toProgress, _ =>
        {
            if (version != _playVersion)
            {
                return;
            }

            VirtualView?.SetIsPlaying(false);
            _playCompletion?.TrySetResult();
            _playCompletion = null;
        });
    }

    private void RemoveAnimationView()
    {
        if (_animationView is null)
        {
            return;
        }

        _animationView.RemoveFromSuperview();
        _animationView.Dispose();
        _animationView = null;
    }

    private static string? ResolveSourcePath(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        const string appPackagePrefix = "ms-appx:///";
        var logicalPath = source.StartsWith(appPackagePrefix, StringComparison.OrdinalIgnoreCase)
            ? source[appPackagePrefix.Length..]
            : source.TrimStart('/');

        if (Path.IsPathRooted(source) && File.Exists(source))
        {
            return source;
        }

        logicalPath = logicalPath.Replace('\\', '/');
        var directory = Path.GetDirectoryName(logicalPath)?.Replace('\\', '/');
        var extension = Path.GetExtension(logicalPath).TrimStart('.');
        var resourceName = Path.GetFileNameWithoutExtension(logicalPath);
        var bundlePath = NSBundle.MainBundle.PathForResource(
            resourceName,
            string.IsNullOrEmpty(extension) ? null : extension,
            string.IsNullOrEmpty(directory) ? null : directory);

        if (!string.IsNullOrEmpty(bundlePath))
        {
            return bundlePath;
        }

        var directPath = Path.Combine(NSBundle.MainBundle.BundlePath, logicalPath);
        return File.Exists(directPath) ? directPath : null;
    }

    private static UIViewContentMode ToContentMode(AnimatedVisualStretch stretch) => stretch switch
    {
        AnimatedVisualStretch.None => UIViewContentMode.Center,
        AnimatedVisualStretch.Fill => UIViewContentMode.ScaleToFill,
        AnimatedVisualStretch.UniformToFill => UIViewContentMode.ScaleAspectFill,
        _ => UIViewContentMode.ScaleAspectFit,
    };
}
