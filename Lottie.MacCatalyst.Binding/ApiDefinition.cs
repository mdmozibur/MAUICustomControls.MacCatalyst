using System;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Lottie.MacCatalyst.Binding;

/// <summary>
/// The Objective-C compatibility surface shipped by Airbnb Lottie 4.6.1.
/// The explicit runtime name is required because the implementation is a Swift class.
/// </summary>
[BaseType(typeof(UIView), Name = "_TtC6Lottie23CompatibleAnimationView")]
[DisableDefaultCtor]
interface CompatibleAnimationView
{
    [Export("initWithData:")]
    NativeHandle Constructor(NSData data);

    [Export("loopAnimationCount")]
    nfloat LoopAnimationCount { get; set; }

    [Export("contentMode")]
    UIViewContentMode ContentMode { get; set; }

    [Export("currentProgress")]
    nfloat CurrentProgress { get; set; }

    [Export("duration")]
    nfloat Duration { get; }

    [Export("animationSpeed")]
    nfloat AnimationSpeed { get; set; }

    [Export("isAnimationPlaying")]
    bool IsAnimationPlaying { get; }

    [Export("play")]
    void Play();

    [Export("playFromProgress:toProgress:completion:")]
    void Play(nfloat fromProgress, nfloat toProgress, [NullAllowed] Action<bool> completion);

    [Export("stop")]
    void Stop();

    [Export("pause")]
    void Pause();
}
