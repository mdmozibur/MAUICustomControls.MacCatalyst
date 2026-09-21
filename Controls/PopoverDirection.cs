namespace MAUICustomControls.MacCatalyst.Controls;
public enum PopoverDirection
{
    Up,
    Down,
    Left,
    Right,
    Auto
}

/// <summary>How a popover lines up with its source along the side it opens on.</summary>
public enum PopoverAlignment
{
    /// <summary>Centred on the source.</summary>
    Center,

    /// <summary>Left (above/below) or top (left/right) edges aligned; the popover extends right or down.</summary>
    Start,

    /// <summary>Right (above/below) or bottom (left/right) edges aligned; the popover extends left or up.</summary>
    End,
}
