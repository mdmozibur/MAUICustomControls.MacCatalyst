using CoreGraphics;
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Platform;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>Size limits and keyboard behaviour of one dialog.</summary>
public sealed record DialogPresentationOptions
{
    public double MinWidth { get; init; } = 320;
    public double MaxWidth { get; init; } = 548;
    public double MinHeight { get; init; }
    public double MaxHeight { get; init; } = 756;

    /// <summary>Return / Enter pressed outside a multi-line text view.</summary>
    public Action? OnEnter { get; init; }

    /// <summary>Escape pressed.</summary>
    public Action? OnEscape { get; init; }

    /// <summary>VoiceOver's name for the dialog (usually its title).</summary>
    public string? AccessibilityLabel { get; init; }
}

/// <summary>
/// A modal dialog on Mac Catalyst: the MAUI content is hosted natively in a card centred over a
/// dimmed window, which blocks the rest of the app until it closes. Replaces pushing a modal MAUI
/// page, which had no dialog surface, no size limits and no keyboard handling.
/// </summary>
public sealed class DialogPresentation
{
    private static int _openCount;

    private readonly DialogHostViewController _controller;
    private readonly DialogTransitioningDelegate _transitioning;
    private bool _dismissed;

    private DialogPresentation(DialogHostViewController controller, DialogTransitioningDelegate transitioning)
    {
        _controller = controller;
        _transitioning = transitioning;
    }

    /// <summary>True while any dialog is on screen (the app menu bar is disabled meanwhile).</summary>
    public static bool IsAnyOpen => Volatile.Read(ref _openCount) > 0;

    /// <summary>Raised after the dialog has left the screen.</summary>
    public event Action? Dismissed;

    public static DialogPresentation? Present(IView content, IMauiContext mauiContext, DialogPresentationOptions options)
    {
        var presenter = PresentationHelpers.GetTopViewController();
        if (presenter is null)
        {
            return null;
        }

        var controller = new DialogHostViewController(content, mauiContext, options);
        var transitioning = new DialogTransitioningDelegate();
        var presentation = new DialogPresentation(controller, transitioning);
        controller.ModalPresentationStyle = UIModalPresentationStyle.Custom;
        controller.TransitioningDelegate = transitioning;
        Interlocked.Increment(ref _openCount);
        presenter.PresentViewController(controller, true, null);
        return presentation;
    }

    /// <summary>Re-measures the content after it changed (for example new button text).</summary>
    public void InvalidateSize() => _controller.UpdatePreferredSize();

    public void Dismiss()
    {
        if (_dismissed)
        {
            return;
        }

        _dismissed = true;
        _controller.DismissViewController(true, () =>
        {
            Interlocked.Decrement(ref _openCount);
            GC.KeepAlive(_transitioning);
            Dismissed?.Invoke();
        });
    }
}

public static class PresentationHelpers
{
    /// <summary>The frontmost view controller of the key window, the one new UI presents from.</summary>
    public static UIViewController? GetTopViewController()
    {
        UIWindow? window = null;
        foreach (var scene in UIApplication.SharedApplication.ConnectedScenes)
        {
            if (scene is UIWindowScene windowScene)
            {
                window = windowScene.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow) ?? window ?? windowScene.Windows.FirstOrDefault();
            }
        }

        var controller = window?.RootViewController;
        while (controller?.PresentedViewController is { IsBeingDismissed: false } presented)
        {
            controller = presented;
        }

        return controller;
    }
}

/// <summary>Hosts the dialog's MAUI view, sizes it within the limits and handles Return/Escape.</summary>
internal sealed class DialogHostViewController : UIViewController
{
    private readonly IView _content;
    private readonly DialogPresentationOptions _options;
    private readonly DialogContentView _contentView;

    public DialogHostViewController(IView content, IMauiContext mauiContext, DialogPresentationOptions options)
    {
        _content = content;
        _options = options;
        _contentView = new DialogContentView(content, content.ToPlatform(mauiContext));
    }

    public DialogPresentationOptions Options => _options;

    public override void LoadView()
    {
        View = _contentView;
        View.AccessibilityViewIsModal = true;
        View.AccessibilityLabel = _options.AccessibilityLabel;
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        UpdatePreferredSize();
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        BecomeFirstResponder();
        // Give keyboard focus to the first text field, as UWP does for a dialog that has one.
        FindFirstTextInput(View!)?.BecomeFirstResponder();
        UIAccessibility.PostNotification(UIAccessibilityPostNotification.ScreenChanged, View);
    }

    public override bool CanBecomeFirstResponder => true;

    public override UIKeyCommand[] KeyCommands =>
    [
        UIKeyCommand.Create((NSString)"\r", (UIKeyModifierFlags)0, new Selector("dialogEnter:")),
        UIKeyCommand.Create(UIKeyCommand.Escape, (UIKeyModifierFlags)0, new Selector("dialogEscape:")),
    ];

    // Return belongs to a multi-line text view (a new line), not to the default button.
    public override bool CanPerform(Selector action, NSObject? withSender)
    {
        if (action.Name == "dialogEnter:")
        {
            return _options.OnEnter is not null && FindFirstResponder(View!) is not UITextView;
        }

        return base.CanPerform(action, withSender);
    }

    [Export("dialogEnter:")]
    public void DialogEnter(UIKeyCommand command) => _options.OnEnter?.Invoke();

    [Export("dialogEscape:")]
    public void DialogEscape(UIKeyCommand command) => _options.OnEscape?.Invoke();

    public void UpdatePreferredSize()
    {
        var container = PresentingViewController?.View?.Bounds.Size ?? UIScreen.MainScreen.Bounds.Size;
        var maxWidth = Math.Min(_options.MaxWidth, container.Width - 2 * DialogPresentationController.WindowMargin);
        var maxHeight = Math.Min(_options.MaxHeight, container.Height - 2 * DialogPresentationController.WindowMargin);

        var desired = _content.Measure(maxWidth, double.PositiveInfinity);
        var width = Math.Clamp(desired.Width, Math.Min(_options.MinWidth, maxWidth), maxWidth);
        var height = _content.Measure(width, double.PositiveInfinity).Height;
        PreferredContentSize = new CGSize(width, Math.Clamp(height, Math.Min(_options.MinHeight, maxHeight), maxHeight));
        (PresentationController as DialogPresentationController)?.ApplyLayout();
    }

    private static UIView? FindFirstTextInput(UIView view)
    {
        if (view is UITextField { Enabled: true } or UITextView { Editable: true })
        {
            return view;
        }

        foreach (var subview in view.Subviews)
        {
            if (FindFirstTextInput(subview) is { } input)
            {
                return input;
            }
        }

        return null;
    }

    private static UIView? FindFirstResponder(UIView view)
    {
        if (view.IsFirstResponder)
        {
            return view;
        }

        foreach (var subview in view.Subviews)
        {
            if (FindFirstResponder(subview) is { } responder)
            {
                return responder;
            }
        }

        return null;
    }
}

/// <summary>An interactive host for a MAUI view that lays it out on every size change.</summary>
internal sealed class DialogContentView : UIView
{
    private readonly IView _view;
    private readonly UIView _platformView;

    public DialogContentView(IView view, UIView platformView)
    {
        _view = view;
        _platformView = platformView;
        AddSubview(platformView);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _view.Measure(Bounds.Width, Bounds.Height);
        _view.Arrange(new Microsoft.Maui.Graphics.Rect(0, 0, Bounds.Width, Bounds.Height));
        _platformView.Frame = Bounds;
    }
}

/// <summary>Dims the window, blocks clicks to it, and centres the dialog card.</summary>
internal sealed class DialogPresentationController : UIPresentationController
{
    public const double WindowMargin = 24;

    private PopoverChromeView? _chrome;
    private UIView? _dimmingView;
    private UIView? _hiddenBehind;
    private bool _hiddenBehindWasHidden;

    public DialogPresentationController(UIViewController presented, UIViewController? presenting)
        : base(presented, presenting)
    {
    }

    public override UIView PresentedView =>
        _chrome ??= new PopoverChromeView(PresentedViewController.View!, new PopoverChromeOptions(10, false, new Thickness(0)));

    public override CGRect FrameOfPresentedViewInContainerView => ComputeFrame();

    public override void PresentationTransitionWillBegin()
    {
        base.PresentationTransitionWillBegin();
        if (ContainerView is not { } container)
        {
            return;
        }

        // Swallows clicks outside the dialog: UWP dialogs are modal, not light-dismiss.
        _dimmingView = new UIView(container.Bounds)
        {
            BackgroundColor = UIColor.Black.ColorWithAlpha(0.25f),
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            Alpha = 0,
        };
        container.InsertSubview(_dimmingView, 0);
        container.AccessibilityViewIsModal = true;

        // On Mac Catalyst the modal flag alone leaves the window content in the accessibility tree,
        // so VoiceOver could still reach the page behind the dialog. Hide it for the dialog's lifetime.
        _hiddenBehind = PresentingViewController?.View;
        if (_hiddenBehind is not null)
        {
            _hiddenBehindWasHidden = _hiddenBehind.AccessibilityElementsHidden;
            _hiddenBehind.AccessibilityElementsHidden = true;
        }

        PresentedViewController.GetTransitionCoordinator()?.AnimateAlongsideTransition(_ => _dimmingView.Alpha = 1, null);
    }

    public override void DismissalTransitionWillBegin()
    {
        base.DismissalTransitionWillBegin();
        PresentedViewController.GetTransitionCoordinator()?.AnimateAlongsideTransition(_ =>
        {
            if (_dimmingView is not null)
            {
                _dimmingView.Alpha = 0;
            }
        }, null);
    }

    public override void DismissalTransitionDidEnd(bool completed)
    {
        base.DismissalTransitionDidEnd(completed);
        if (completed)
        {
            _dimmingView?.RemoveFromSuperview();
            _dimmingView = null;
            if (_hiddenBehind is not null)
            {
                _hiddenBehind.AccessibilityElementsHidden = _hiddenBehindWasHidden;
                _hiddenBehind = null;
            }

            UIAccessibility.PostNotification(UIAccessibilityPostNotification.ScreenChanged, null);
        }
    }

    public override void ContainerViewWillLayoutSubviews()
    {
        base.ContainerViewWillLayoutSubviews();
        ApplyLayout();
    }

    public override void PreferredContentSizeDidChangeForChildContentContainer(IUIContentContainer container)
    {
        base.PreferredContentSizeDidChangeForChildContentContainer(container);
        ContainerView?.SetNeedsLayout();
    }

    internal void ApplyLayout()
    {
        if (_chrome is null || ContainerView is null)
        {
            return;
        }

        var frame = ComputeFrame();
        _chrome.Bounds = new CGRect(0, 0, frame.Width, frame.Height);
        _chrome.Center = new CGPoint(frame.GetMidX(), frame.GetMidY());
        _chrome.SetNeedsLayout();
    }

    private CGRect ComputeFrame()
    {
        if (ContainerView is not { } container)
        {
            return CGRect.Empty;
        }

        var size = PresentedViewController.PreferredContentSize;
        var bounds = container.Bounds;
        var insets = container.SafeAreaInsets;
        var width = Math.Min(size.Width, bounds.Width - 2 * WindowMargin);
        var height = Math.Min(size.Height, bounds.Height - insets.Top - 2 * WindowMargin);
        var x = (bounds.Width - width) / 2;
        var y = insets.Top + (bounds.Height - insets.Top - height) / 2;
        return new CGRect(x, y, width, height);
    }
}

internal sealed class DialogTransitioningDelegate : UIViewControllerTransitioningDelegate
{
    public override UIPresentationController GetPresentationControllerForPresentedViewController(
        UIViewController presentedViewController,
        UIViewController? presentingViewController,
        UIViewController sourceViewController) =>
        new DialogPresentationController(presentedViewController, presentingViewController);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForPresentedController(
        UIViewController presented, UIViewController presenting, UIViewController source) =>
        new DialogAnimator(presenting: true);

    public override IUIViewControllerAnimatedTransitioning GetAnimationControllerForDismissedController(UIViewController dismissed) =>
        new DialogAnimator(presenting: false);
}

/// <summary>The WinUI dialog motion: a short fade in with a slight scale up, reversed on close.</summary>
internal sealed class DialogAnimator : UIViewControllerAnimatedTransitioning
{
    private const float CollapsedScale = 1.05f;
    private readonly bool _presenting;

    public DialogAnimator(bool presenting) => _presenting = presenting;

    public override double TransitionDuration(IUIViewControllerContextTransitioning? transitionContext) => _presenting ? 0.16 : 0.12;

    public override void AnimateTransition(IUIViewControllerContextTransitioning context)
    {
        var key = _presenting ? UITransitionContext.ToViewControllerKey : UITransitionContext.FromViewControllerKey;
        var controller = context.GetViewControllerForKey(key);
        var presentation = controller?.PresentationController as DialogPresentationController;
        var view = presentation?.PresentedView;
        if (controller is null || presentation is null || view is null)
        {
            context.CompleteTransition(true);
            return;
        }

        if (_presenting)
        {
            context.ContainerView.AddSubview(view);
            presentation.ApplyLayout();
            view.LayoutIfNeeded();
            view.Alpha = 0;
            view.Transform = CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);
        }

        UIView.Animate(TransitionDuration(context), 0, _presenting ? UIViewAnimationOptions.CurveEaseOut : UIViewAnimationOptions.CurveEaseIn,
            () =>
            {
                view.Alpha = _presenting ? 1 : 0;
                view.Transform = _presenting ? CGAffineTransform.MakeIdentity() : CGAffineTransform.MakeScale(CollapsedScale, CollapsedScale);
            },
            () =>
            {
                if (!_presenting)
                {
                    view.RemoveFromSuperview();
                }

                context.CompleteTransition(!context.TransitionWasCancelled);
            });
    }
}
