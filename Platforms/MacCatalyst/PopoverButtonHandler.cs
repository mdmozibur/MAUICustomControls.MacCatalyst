using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst
{
    public class PopoverButtonHandler : ContentViewHandler
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
                popoverButton.HidePopoverAction = () =>
                {
                    if (_popoverController != null && _popoverController.TryGetTarget(out var controller) && controller.IsViewLoaded && controller.View.Window != null)
                    {
                        controller.DismissViewController(true, CleanupPopover);
                    }
                };
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
            }
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
            if (VirtualView is not PopoverButton popoverButton || popoverButton.PopoverContent == null)
                return;

            var popoverContent = popoverButton.PopoverContent;
            var mauiContext = MauiContext ?? throw new InvalidOperationException("MauiContext is null");

            var viewController = new UIViewController
            {
                ModalPresentationStyle = UIModalPresentationStyle.Popover,
                View = popoverContent.ToPlatform(mauiContext)
            };
            var measure = popoverContent.Measure(double.PositiveInfinity, double.PositiveInfinity);
            viewController.PreferredContentSize = new CoreGraphics.CGSize(measure.Width, measure.Height);
            _popoverController = new WeakReference<UIViewController>(viewController);

            var popover = viewController.PopoverPresentationController;
            if (popover != null)
            {
                popover.SourceView = PlatformView;
                popover.SourceRect = PlatformView.Bounds;
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

            var rootViewController = UIApplication.SharedApplication.KeyWindow?.RootViewController;
            rootViewController?.PresentViewController(viewController, true, null);
        }

        internal void CleanupPopover()
        {
            if (VirtualView is PopoverButton popoverButton && popoverButton.PopoverContent?.Handler is not null)
            {
                popoverButton.PopoverContent.Handler.DisconnectHandler();
            }
            _popoverController = null;
        }
    }
}