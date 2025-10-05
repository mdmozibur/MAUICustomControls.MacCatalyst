
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst
{
    public class PopoverDelegate : UIPopoverPresentationControllerDelegate
    {
        private readonly WeakReference<PopoverButtonHandler> _handler;

        public PopoverDelegate(PopoverButtonHandler handler)
        {
            _handler = new WeakReference<PopoverButtonHandler>(handler);
        }

        public override void DidDismissPopover(UIPopoverPresentationController popoverPresentationController)
        {
            // Call the handler's cleanup method when the user dismisses the popover
            if (_handler.TryGetTarget(out var handler))
            {
                handler.CleanupPopover();
            }
        }
    }
}