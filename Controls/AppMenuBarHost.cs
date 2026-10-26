namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// Where UWP's MenuBar was in the page. The converter moves the menus to the page's MenuBarItems:
/// the Mac shows them in the macOS menu bar and leaves this empty; Linux, with no system menu bar
/// to put them in, shows them here as an in-window menu bar.
/// </summary>
public class AppMenuBarHost : ContentView
{
}
