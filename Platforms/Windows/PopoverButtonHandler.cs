using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using MauiPopoverButton.Controls;

namespace MAUICustomControls.MacCatalyst.Platforms.Windows
{
    public class PopoverButtonHandler : ViewHandler<PopoverButton, Button>
    {
        public static PropertyMapper<PopoverButton, PopoverButtonHandler> PropertyMapper = new(ViewHandler.ViewMapper)
        {
            [nameof(PopoverButton.ButtonContent)] = MapButtonContent,
            [nameof(PopoverButton.PopoverContent)] = MapPopoverContent,
            [nameof(PopoverButton.BorderColor)] = MapBorderColor,
            [nameof(PopoverButton.BorderWidth)] = MapBorderWidth,
            [nameof(PopoverButton.PopoverDirection)] = MapPopoverDirection,
            [nameof(PopoverButton.Padding)] = MapPadding,
        };

        public PopoverButtonHandler() : base(PropertyMapper)
        {
        }

        protected override Button CreatePlatformView()
        {
            return new Button();
        }
        protected override void ConnectHandler(Button platformView)
        {
            base.ConnectHandler(platformView);
            // Assign the action to hide the flyout
            VirtualView?.HidePopoverAction = () =>
            {
                PlatformView?.Flyout?.Hide();
            };
        }

        protected override void DisconnectHandler(Button platformView)
        {
            VirtualView?.HidePopoverAction = null;
            base.DisconnectHandler(platformView);
        }


        private static void MapPadding(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            handler.PlatformView.Padding = popoverButton.Padding.ToPlatform();
        }
        
        private static void MapPopoverDirection(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            if (handler.PlatformView.Flyout is Flyout flyout)
            {
                flyout.Placement = popoverButton.PopoverDirection switch
                {
                    PopoverDirection.Up => FlyoutPlacementMode.Top,
                    PopoverDirection.Down => FlyoutPlacementMode.Bottom,
                    PopoverDirection.Left => FlyoutPlacementMode.Left,
                    PopoverDirection.Right => FlyoutPlacementMode.Right,
                    _ => FlyoutPlacementMode.Bottom
                };
            }
        }
        private static void MapButtonContent(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            handler.PlatformView.Content = popoverButton.ButtonContent?.ToPlatform(handler.MauiContext);
        }

        private static void MapPopoverContent(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            if (popoverButton.PopoverContent != null)
            {
                var flyout = new Flyout
                {
                    Content = popoverButton.PopoverContent.ToPlatform(handler.MauiContext)
                };
                handler.PlatformView.Flyout = flyout;
            }
        }



        private static void MapBorderColor(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            handler.PlatformView.BorderBrush = popoverButton.BorderColor?.ToPlatform();
        }

        private static void MapBorderWidth(PopoverButtonHandler handler, PopoverButton popoverButton)
        {
            handler.PlatformView.BorderThickness = new Thickness(popoverButton.BorderWidth);
        }
    }
}