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
            ["UseSystemFocusVisuals"] = (handler, view) => handler.PlatformView.SetNeedsLayout(),
            [nameof(PopoverButton.BorderColor)] = MapBorder,
            [nameof(PopoverButton.BorderWidth)] = MapBorder,
            [nameof(IContentView.Content)] = MapContent,
        };

        public PopoverButtonHandler() : base(PropertyMapper)
        {
        }

        private bool IsPopoverPresented => _popoverController?.PresentingViewController is not null;

        // A button to VoiceOver and to keyboard navigation, which a tap recognizer alone is not.
        protected override Microsoft.Maui.Platform.ContentView CreatePlatformView() =>
            new PopoverButtonView(() => OnTapped()) { CrossPlatformLayout = VirtualView, FocusVisualsOwner = VirtualView as Microsoft.Maui.Controls.BindableObject };

        protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
        {
            base.ConnectHandler(platformView);
            _tapGestureRecognizer = new UITapGestureRecognizer(OnTapped);
            PlatformView.AddGestureRecognizer(_tapGestureRecognizer);
            PlatformView.UserInteractionEnabled = true;

            if (VirtualView is PopoverButton popoverButton)
            {
                popoverButton.HidePopoverAction = () => HideActivePopover(animated: true);
                // Listened to rather than mapped: mapping IsEnabled here would replace MAUI's own mapping.
                popoverButton.PropertyChanged += PopoverButton_PropertyChanged;
                MapAccessibility(this, popoverButton);
            }
        }

        private void PopoverButton_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PopoverButton popoverButton && e.PropertyName is nameof(PopoverButton.IsEnabled) or nameof(PopoverButton.Text) or "Description" or "Hint" or "Text")
            {
                MapAccessibility(this, popoverButton);
            }
        }

        protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
        {
            if (VirtualView is PopoverButton popoverButton)
            {
                popoverButton.HidePopoverAction = null;
                popoverButton.PropertyChanged -= PopoverButton_PropertyChanged;
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

        private static void MapAccessibility(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            var view = handler.PlatformView;
            view.IsAccessibilityElement = true;
            view.AccessibilityTraits = popoverButton.IsEnabled
                ? UIAccessibilityTrait.Button
                : UIAccessibilityTrait.Button | UIAccessibilityTrait.NotEnabled;

            // An explicit description wins; otherwise the button's text, then its tooltip (icon buttons).
            view.AccessibilityLabel = FirstNonEmpty(
                Microsoft.Maui.Controls.SemanticProperties.GetDescription(popoverButton),
                popoverButton.Text,
                popoverButton.Content as string,
                Microsoft.Maui.Controls.ToolTipProperties.GetText(popoverButton)?.ToString());
            view.AccessibilityHint = Microsoft.Maui.Controls.SemanticProperties.GetHint(popoverButton);

            // A button that opens a flyout reads as collapsed or expanded, like a pop-up button.
            if (OperatingSystem.IsMacCatalystVersionAtLeast(18))
            {
                view.AccessibilityExpandedStatus = popoverButton.GetPresentedContent() is null
                    ? UIAccessibilityExpandedStatus.Unsupported
                    : handler.IsPopoverPresented ? UIAccessibilityExpandedStatus.Expanded : UIAccessibilityExpandedStatus.Collapsed;
            }
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private void OnTapped()
        {
            if (VirtualView is not PopoverButton { IsEnabled: true } popoverButton)
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
                popoverButton.PopoverAlignment,
                new PopoverChromeOptions(popoverButton.PopoverCornerRadius, popoverButton.ShowPopoverArrow, popoverButton.PopoverPadding),
                onOutsideTap: () => HideActivePopover(animated: true));
            controller.TransitioningDelegate = _transitioningDelegate;

            _isDismissing = false;

            // Opened is raised once the popover is on screen, as UWP's Flyout does. Raising it
            // before presenting ran its handlers (e.g. a canvas redraw) ahead of the animation.
            presentingController.PresentViewController(controller, true, () =>
            {
                MapAccessibility(this, popoverButton);
                popoverButton.RaiseOpened();
            });
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
            if (VirtualView is PopoverButton popoverButton)
            {
                MapAccessibility(this, popoverButton);
                popoverButton.RaiseClosed();
            }
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

    /// <summary>
    /// The button's native view: activated by VoiceOver and, when it has keyboard focus (Tab with
    /// keyboard navigation on), by Space or Return, like a native button.
    /// </summary>
    internal sealed class PopoverButtonView : Microsoft.Maui.Platform.ContentView
    {
        private readonly Action _activate;

        public PopoverButtonView(Action activate) => _activate = activate;

        /// <summary>The element whose UseSystemFocusVisuals decides whether the focus ring is drawn.</summary>
        public Microsoft.Maui.Controls.BindableObject? FocusVisualsOwner { get; set; }

        public override bool CanBecomeFocused => UserInteractionEnabled && !AccessibilityTraits.HasFlag(UIAccessibilityTrait.NotEnabled);

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            FocusRing.UpdateCustomView(this, FocusVisualsOwner);
        }

        public override bool AccessibilityActivate()
        {
            _activate();
            return true;
        }

        public override void PressesBegan(Foundation.NSSet<UIPress> presses, UIPressesEvent evt)
        {
            if (Focused && presses.ToArray().Any(press => press.Key?.KeyCode is UIKeyboardHidUsage.KeyboardSpacebar or UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKeyboardHidUsage.KeypadEnter))
            {
                _activate();
                return;
            }

            base.PressesBegan(presses, evt);
        }
    }
}
