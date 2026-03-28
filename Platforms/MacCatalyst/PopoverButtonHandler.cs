using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst
{
    public sealed class PopoverButtonHandler : ContentViewHandler
    {
        private UITapGestureRecognizer? _tapGestureRecognizer;
        private PopoverDelegate? _popoverDelegate;
        private WeakReference<UIViewController>? _popoverController;

        public static PropertyMapper<PopoverButton, PopoverButtonHandler> PropertyMapper = new(ViewMapper)
        {
            [nameof(PopoverButton.BorderColor)] = MapBorder,
            [nameof(PopoverButton.BorderWidth)] = MapBorder,
            [nameof(IContentView.Content)] = MapContent
        };

        public PopoverButtonHandler() : base(PropertyMapper)
        {
        }

        protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
        {
            base.ConnectHandler(platformView);
            _tapGestureRecognizer = new UITapGestureRecognizer(OnTapped);
            PlatformView.AddGestureRecognizer(_tapGestureRecognizer);
            PlatformView.UserInteractionEnabled = true;

            if (VirtualView is PopoverButton popoverButton)
            {
                popoverButton.HidePopoverAction = HideActivePopover;
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

            HideActivePopover();
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

            if (TryGetActivePopover(out var activePopover))
            {
                activePopover.DismissViewController(true, CleanupPopover);
                return;
            }

            var popoverContent = presentedContent;
            var mauiContext = MauiContext ?? throw new InvalidOperationException("MauiContext is null");

            var viewController = new UIViewController
            {
                ModalPresentationStyle = UIModalPresentationStyle.Popover,
                View = popoverContent.ToPlatform(mauiContext)
            };
            var measure = popoverContent.Measure(double.PositiveInfinity, double.PositiveInfinity);
            viewController.PreferredContentSize = new CoreGraphics.CGSize(Math.Max(1, measure.Width), Math.Max(1, measure.Height));
            _popoverController = new WeakReference<UIViewController>(viewController);

            var popover = viewController.PopoverPresentationController;
            if (popover != null)
            {
                popover.SourceView = PlatformView;
                popover.SourceRect = PlatformView.Bounds.IsEmpty ? new CoreGraphics.CGRect(0, 0, PlatformView.Frame.Width, PlatformView.Frame.Height) : PlatformView.Bounds;
                popover.PermittedArrowDirections = popoverButton.PopoverDirection switch
                {
                    PopoverDirection.Up => UIPopoverArrowDirection.Down,
                    PopoverDirection.Down => UIPopoverArrowDirection.Up,
                    PopoverDirection.Left => UIPopoverArrowDirection.Right,
                    PopoverDirection.Right => UIPopoverArrowDirection.Left,
                    _ => UIPopoverArrowDirection.Any
                };

                _popoverDelegate = new PopoverDelegate(this);
                popover.Delegate = _popoverDelegate;
            }

            var presentingController = GetPresentingViewController();
            presentingController?.PresentViewController(viewController, true, null);
        }

        internal void CleanupPopover()
        {
            if (VirtualView is PopoverButton popoverButton)
            {
                if (popoverButton.PopoverContent?.Handler is not null)
                {
                    popoverButton.PopoverContent.Handler.DisconnectHandler();
                }
            }

            _popoverDelegate = null;
            _popoverController = null;
        }

        private void HideActivePopover()
        {
            if (TryGetActivePopover(out var controller))
            {
                controller.DismissViewController(true, CleanupPopover);
            }
        }

        private bool TryGetActivePopover(out UIViewController controller)
        {
            if (_popoverController != null && _popoverController.TryGetTarget(out var currentController) && currentController.PresentingViewController != null)
            {
                controller = currentController;
                return true;
            }

            controller = null!;
            return false;
        }

        private static UIViewController? GetPresentingViewController()
        {
            foreach (var scene in UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>())
            {
                var window = scene.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow) ?? scene.Windows.FirstOrDefault();
                if (window?.RootViewController is UIViewController rootViewController)
                {
                    return GetTopViewController(rootViewController);
                }
            }

            return null;
        }

        private static UIViewController GetTopViewController(UIViewController controller)
        {
            var current = controller;
            while (current.PresentedViewController is UIViewController presented)
            {
                current = presented;
            }

            return current;
        }
    }
}