using System;
using MAUICustomControls.MacCatalyst.Controls;

namespace MAUICustomControls.MacCatalyst;

public interface IPopoverButtonHandler : IViewHandler
{
    PopoverButton VirtualView { get; }
    // Potentially add methods here if you need to expose platform-specific actions
}

