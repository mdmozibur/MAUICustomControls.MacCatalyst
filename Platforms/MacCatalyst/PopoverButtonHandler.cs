using CoreFoundation;
using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst
{
    public sealed class PopoverButtonHandler : ContentViewHandler
    {
        private UITapGestureRecognizer? _tapGestureRecognizer;

        // Built on first open and reused afterwards, so reopening does not rebuild the
        // content's native views. Released when the handler disconnects.
        private PopoverHostViewController? _popoverController;
        private Microsoft.Maui.Controls.View? _popoverControllerContent;

        // Held in a field: TransitioningDelegate is a weak reference on the ObjC side.
        private PopoverTransitioningDelegate? _transitioningDelegate;
        private bool _isDismissing;
        private bool _contentSizeUpdatePending;

        public static PropertyMapper<PopoverButton, PopoverButtonHandler> PropertyMapper = new(ViewMapper)
        {
            [nameof(PopoverButton.BorderColor)] = MapBorder,
            [nameof(PopoverButton.BorderWidth)] = MapBorder,
            [nameof(IContentView.Content)] = MapContent
        };

        public PopoverButtonHandler() : base(PropertyMapper)
        {
        }

        private bool IsPopoverPresented => _popoverController?.PresentingViewController is not null;

        protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
        {
            base.ConnectHandler(platformView);
            _tapGestureRecognizer = new UITapGestureRecognizer(OnTapped);
            PlatformView.AddGestureRecognizer(_tapGestureRecognizer);
            PlatformView.UserInteractionEnabled = true;

            if (VirtualView is PopoverButton popoverButton)
            {
                popoverButton.HidePopoverAction = () => HideActivePopover(animated: true);
            }
        }

        protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
        {
            if (VirtualView is PopoverButton popoverButton)
            {
                popoverButton.HidePopoverAction = null;
            }

            if (_tapGestureRecognizer != null)
            {
                platformView.RemoveGestureRecognizer(_tapGestureRecognizer);
                _tapGestureRecognizer.Dispose();
                _tapGestureRecognizer = null;
            }

            HideActivePopover(animated: false);
            ReleasePopoverController();
            base.DisconnectHandler(platformView);
        }

        private static void MapBorder(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            handler.PlatformView.Layer.BorderColor = popoverButton.BorderColor.ToCGColor();
            handler.PlatformView.Layer.BorderWidth = (nfloat)popoverButton.BorderWidth;
            // handler.PlatformView.Layer.CornerRadius = popoverButton.CornerRadius.TopLeft;
        }

        private void OnTapped()
        {
            if (VirtualView is not PopoverButton popoverButton)
                return;

            popoverButton.RaiseClicked();

            var presentedContent = popoverButton.GetPresentedContent();
            if (presentedContent == null)
                return;

            if (IsPopoverPresented)
            {
                HideActivePopover(animated: true);
                return;
            }

            var presentingController = GetPresentingViewController();
            if (presentingController is null)
                return;

            popoverButton.RaiseOpening();

            var controller = GetOrCreatePopoverController(presentedContent);
            UpdatePreferredContentSize(controller, presentedContent, popoverButton.PopoverPadding);

            controller.ModalPresentationStyle = UIModalPresentationStyle.Custom;
            _transitioningDelegate = new PopoverTransitioningDelegate(
                PlatformView,
                popoverButton.PopoverDirection,
                new PopoverChromeOptions(popoverButton.PopoverCornerRadius, popoverButton.ShowPopoverArrow, popoverButton.PopoverPadding),
                onOutsideTap: () => HideActivePopover(animated: true));
            controller.TransitioningDelegate = _transitioningDelegate;

            _isDismissing = false;

            // Opened is raised once the popover is on screen, as UWP's Flyout does. Raising it
            // before presenting ran its handlers (e.g. a canvas redraw) ahead of the animation.
            presentingController.PresentViewController(controller, true, () => popoverButton.RaiseOpened());
        }

        private PopoverHostViewController GetOrCreatePopoverController(Microsoft.Maui.Controls.View content)
        {
            if (_popoverController is not null && ReferenceEquals(_popoverControllerContent, content))
                return _popoverController;

            ReleasePopoverController();

            var mauiContext = MauiContext ?? throw new InvalidOperationException("MauiContext is null");
            _popoverController = new PopoverHostViewController(
                content.ToPlatform(mauiContext),
                onEscape: () => HideActivePopover(animated: true));
            _popoverControllerContent = content;
            content.MeasureInvalidated += PopoverContent_MeasureInvalidated;

            return _popoverController;
        }

        private void ReleasePopoverController()
        {
            if (_popoverControllerContent is not null)
            {
                _popoverControllerContent.MeasureInvalidated -= PopoverContent_MeasureInvalidated;
                _popoverControllerContent.Handler?.DisconnectHandler();
            }

            _popoverControllerContent = null;
            _popoverController = null;
            _transitioningDelegate = null;
        }

        private static void UpdatePreferredContentSize(UIViewController controller, Microsoft.Maui.Controls.View content, Thickness padding)
        {
            // The unconstrained pass only settles the width. Arranged at that width, star columns
            // split it evenly and text can wrap (e.g. "From color book" in SCColorChooser), so the
            // height has to come from a second pass at the final width.
            var width = Math.Ceiling(content.Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
            var height = Math.Ceiling(content.Measure(width, double.PositiveInfinity).Height);
            var size = new CGSize(
                Math.Max(1, width + padding.HorizontalThickness),
                Math.Max(1, height + padding.VerticalThickness));

            if (controller.PreferredContentSize != size)
                controller.PreferredContentSize = size;
        }

        private void PopoverContent_MeasureInvalidated(object? sender, EventArgs e)
        {
            if (_contentSizeUpdatePending || !IsPopoverPresented)
                return;

            // A single visibility change invalidates every ancestor; re-measure once for the burst.
            _contentSizeUpdatePending = true;
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                _contentSizeUpdatePending = false;

                if (IsPopoverPresented
                    && _popoverController is { } controller
                    && _popoverControllerContent is { } content
                    && VirtualView is PopoverButton popoverButton)
                {
                    UpdatePreferredContentSize(controller, content, popoverButton.PopoverPadding);
                }
            });
        }

        private void HideActivePopover(bool animated)
        {
            if (_isDismissing || _popoverController is not { PresentingViewController: not null } controller)
                return;

            _isDismissing = true;
            controller.DismissViewController(animated, OnPopoverDismissed);
        }

        private void OnPopoverDismissed()
        {
            _isDismissing = false;
            _transitioningDelegate = null;
            (VirtualView as PopoverButton)?.RaiseClosed();
        }

        private UIViewController? GetPresentingViewController()
        {
            var window = PlatformView.Window ?? UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(candidate => candidate.IsKeyWindow);

            var controller = window?.RootViewController;

            while (controller?.PresentedViewController is not null)
                controller = controller.PresentedViewController;

            return controller;
        }
    }
}
