namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Microsoft.Maui;

public sealed class LinuxLabelHandler : Avalonia.Controls.Maui.Handlers.LabelHandler, IViewHandler
{
    Microsoft.Maui.Graphics.Size IViewHandler.GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        var desired = base.GetDesiredSize(widthConstraint, heightConstraint);
        // The backend measures the live TextBlock. Measurement changes its text
        // alignment constraint even when the containing MAUI panel keeps its
        // previous size and native arrange is skipped. Restore the final frame
        // after measuring an already-arranged label, retaining the measured size.
        if (PlatformView is { Bounds.Width: > 0, Bounds.Height: > 0, IsArrangeValid: false })
        {
            // A label with a background is hosted inside a native container.
            // Its cached arrangement must also allow the text child to arrange.
            ContainerView?.InvalidateArrange();
            base.PlatformArrange(VirtualView.Frame);
        }
        return desired;
    }
}
