using System.Runtime.InteropServices;
using Foundation;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public enum AppCloseRequest
{
    /// <summary>Quit menu item, Cmd-Q, Dock, logout.</summary>
    Quit,

    /// <summary>The window's close button or Cmd-W.</summary>
    CloseWindow,
}

public enum AppKitKeyEventKind
{
    KeyDown,
    KeyUp,
    FlagsChanged,
}

/// <summary>
/// One AppKit key event: the Mac virtual key code (kVK_*), NSEventModifierFlags (the same bits as
/// UIKeyModifierFlags), whether it is an auto-repeat, and the first character of
/// charactersIgnoringModifiers ('\0' for modifier-flag changes).
/// </summary>
public readonly record struct AppKitKeyEvent(AppKitKeyEventKind Kind, ushort KeyCode, ulong ModifierFlags, bool IsRepeat, char Character);

public enum AppKitMouseEventKind
{
    Down,
    Dragged,
    Up,

    /// <summary>
    /// The pointer moved inside the title bar band (UIKit sends no hover there), or left it (X and Y
    /// are -1). Raised through <see cref="AppKitBridge.TitleBandPointerMoved"/>.
    /// </summary>
    TitleBandMoved,
}

/// <summary>
/// A right or other (middle, side) mouse button event, which UIKit never delivers on Mac Catalyst.
/// ButtonNumber is AppKit's (1 right, 2 middle, 3 and 4 the side buttons). X and Y are in the
/// window's content view, top-left origin, in AppKit points; ContentWidth and ContentHeight are that
/// view's size, so a UIKit window point is X * window.Bounds.Width / ContentWidth.
/// </summary>
public readonly record struct AppKitMouseEvent(AppKitMouseEventKind Kind, int ButtonNumber, double X, double Y, double ContentWidth, double ContentHeight, ulong ModifierFlags, int ClickCount);

/// <summary>A full-screen transition or screen change of the app's window (AppKit notifications).</summary>
public enum AppKitWindowEvent
{
    WillEnterFullScreen = 0,
    DidEnterFullScreen = 1,
    WillExitFullScreen = 2,
    DidExitFullScreen = 3,
    ScreenChanged = 4,
}

/// <summary>
/// The main window's chrome in AppKit points (= UIKit points in the Mac idiom): the height of the
/// title bar band, the width the traffic-light buttons take from the leading edge (0 in full screen),
/// whether the window is in full screen, and the visible frame (no menu bar or Dock) of its screen in
/// UIKit system coordinates (top-left origin).
/// </summary>
public readonly record struct AppKitWindowChrome(double TitleBarHeight, double LeadingInset, bool IsFullScreen, double ScreenX, double ScreenY, double ScreenWidth, double ScreenHeight);

/// <summary>The NSCursor shapes <see cref="AppKitBridge.SetCursor"/> can show.</summary>
public enum AppKitCursor
{
    /// <summary>No override: UIKit manages the pointer again (the arrow over plain views).</summary>
    System = -1,
    Arrow = 0,
    IBeam = 1,
    Crosshair = 2,
    PointingHand = 3,
    OpenHand = 4,
    ClosedHand = 5,
    ResizeLeftRight = 6,
    ResizeUpDown = 7,
    ResizeNorthwestSoutheast = 8,
    ResizeNortheastSouthwest = 9,
    OperationNotAllowed = 10,
    ResizeUp = 11,
    ContextualMenu = 12,
    Hidden = 13,
}

// AppKit services UIKit does not offer on Mac Catalyst, from Native/AppKitBridge: a macOS plug-in
// bundle (build it with Native/AppKitBridge/build_bundle.sh) that the app ships in
// Contents/PlugIns and loads at run time. Every call is a no-op when the plug-in is missing.
public static unsafe class AppKitBridge
{
    private static readonly Lazy<IntPtr> Library = new(Load);
    private static Func<AppCloseRequest, bool>? _canClose;
    private static Func<AppKitKeyEvent, bool>? _keyEvent;
    private static Action<AppKitMouseEvent>? _mouseEvent;
    private static Action<AppKitWindowEvent>? _windowEvent;

    public static bool IsAvailable => Library.Value != IntPtr.Zero;

    /// <summary>
    /// The pointer moved in the title bar band of a window whose content extends under it, or left
    /// the band (<see cref="AppKitMouseEventKind.TitleBandMoved"/>). Reported while the mouse
    /// monitor (<see cref="InstallMouseMonitor"/>) is installed.
    /// </summary>
    public static event Action<AppKitMouseEvent>? TitleBandPointerMoved;

    /// <summary>
    /// Asks <paramref name="canClose"/> before the app quits (Quit, Cmd-Q, Dock, logout) or its
    /// window closes (close button, Cmd-W). Returning false cancels; the app then asks the user and,
    /// when it may go ahead, calls <see cref="TerminateNow"/>. Call once a window exists.
    /// </summary>
    public static bool InstallCloseGuards(Func<AppCloseRequest, bool> canClose)
    {
        if (!TryGetExport("appkit_install_close_guards", out var install))
        {
            return false;
        }

        _canClose = canClose;
        return ((delegate* unmanaged<delegate* unmanaged<int>, delegate* unmanaged<nint, int>, int>)install)(&ShouldTerminate, &ShouldCloseWindow) != 0;
    }

    /// <summary>
    /// Sends every key event of the app's windows to <paramref name="keyEvent"/> before AppKit or
    /// UIKit dispatches it (a focused UIKit text field consumes keys without UIKit ever reporting
    /// them). Returning true swallows the event. Returns false when the plug-in is missing.
    /// </summary>
    public static bool InstallKeyMonitor(Func<AppKitKeyEvent, bool> keyEvent)
    {
        if (!TryGetExport("appkit_install_key_monitor", out var install))
        {
            return false;
        }

        _keyEvent = keyEvent;
        return ((delegate* unmanaged<delegate* unmanaged<int, int, nuint, int, int, int>, int>)install)(&OnKeyEvent) != 0;
    }

    /// <summary>
    /// Sends every right and other (middle, side) mouse button press, drag and release in the app's
    /// windows to <paramref name="mouseEvent"/>; UIKit never turns them into touches. The events still
    /// reach AppKit and UIKit afterwards. Returns false when the plug-in is missing.
    /// </summary>
    public static bool InstallMouseMonitor(Action<AppKitMouseEvent> mouseEvent)
    {
        if (!TryGetExport("appkit_install_mouse_monitor", out var install))
        {
            return false;
        }

        _mouseEvent = mouseEvent;
        return ((delegate* unmanaged<delegate* unmanaged<int, int, double, double, double, double, nuint, int, void>, int>)install)(&OnMouseEvent) != 0;
    }

    /// <summary>
    /// Shows <paramref name="cursor"/> over the app's windows until the next call; the mouse monitor
    /// sets it again whenever UIKit resets the pointer. <see cref="AppKitCursor.System"/> hands the
    /// pointer back to UIKit. Returns false when the plug-in is missing.
    /// </summary>
    public static bool SetCursor(AppKitCursor cursor)
    {
        if (!TryGetExport("appkit_set_cursor", out var setCursor))
        {
            return false;
        }

        ((delegate* unmanaged<int, void>)setCursor)((int)cursor);
        return true;
    }

    /// <summary>
    /// The accent colour chosen in System Settings > Appearance (NSColor.controlAccentColor), as sRGB
    /// components from 0 to 1. UIKit does not report it to an iPad-idiom Catalyst app, whose tint stays
    /// system blue. Returns false when the plug-in is missing.
    /// </summary>
    public static bool TryGetAccentColor(out double red, out double green, out double blue)
    {
        red = green = blue = 0;
        if (!TryGetExport("appkit_get_accent_color", out var getAccent))
        {
            return false;
        }

        double r, g, b;
        var found = ((delegate* unmanaged<double*, double*, double*, int>)getAccent)(&r, &g, &b) != 0;
        if (found)
        {
            (red, green, blue) = (r, g, b);
        }

        return found;
    }

    /// <summary>
    /// Enters or leaves full screen (NSWindow toggleFullScreen:, which UIKit has no API for). Returns
    /// true when the window is already in that state or the transition started.
    /// </summary>
    public static bool SetFullScreen(bool enter)
    {
        if (!TryGetExport("appkit_set_full_screen", out var setFullScreen))
        {
            return false;
        }

        return ((delegate* unmanaged<int, int>)setFullScreen)(enter ? 1 : 0) != 0;
    }

    /// <summary>Reads the main window's chrome; false when the plug-in or the window is missing.</summary>
    public static bool TryGetWindowChrome(out AppKitWindowChrome chrome)
    {
        chrome = default;
        if (!TryGetExport("appkit_get_window_chrome", out var getChrome))
        {
            return false;
        }

        double height, inset, x, y, width, screenHeight;
        int fullScreen;
        var found = ((delegate* unmanaged<double*, double*, int*, double*, double*, double*, double*, int>)getChrome)(
            &height, &inset, &fullScreen, &x, &y, &width, &screenHeight) != 0;
        if (found)
        {
            chrome = new AppKitWindowChrome(height, inset, fullScreen != 0, x, y, width, screenHeight);
        }

        return found;
    }

    /// <summary>
    /// The visible frame (no menu bar or Dock) of the screen that holds most of the given rectangle,
    /// both in UIKit system coordinates (top-left origin), and whether the rectangle is on a screen at
    /// all (otherwise the main screen's). False when the plug-in is missing.
    /// </summary>
    public static bool TryGetVisibleFrame(double x, double y, double width, double height, out (double X, double Y, double Width, double Height) visibleFrame, out bool onScreen)
    {
        visibleFrame = default;
        onScreen = false;
        if (!TryGetExport("appkit_get_visible_frame", out var getFrame))
        {
            return false;
        }

        double sx, sy, sw, sh;
        onScreen = ((delegate* unmanaged<double, double, double, double, double*, double*, double*, double*, int>)getFrame)(
            x, y, width, height, &sx, &sy, &sw, &sh) != 0;
        visibleFrame = (sx, sy, sw, sh);
        return sw > 0 && sh > 0;
    }

    /// <summary>
    /// Reports the app window's full-screen transitions and screen changes to
    /// <paramref name="windowEvent"/>. Returns false when the plug-in is missing.
    /// </summary>
    public static bool InstallWindowObserver(Action<AppKitWindowEvent> windowEvent)
    {
        if (!TryGetExport("appkit_install_window_observer", out var install))
        {
            return false;
        }

        _windowEvent = windowEvent;
        return ((delegate* unmanaged<delegate* unmanaged<int, void>, int>)install)(&OnWindowEvent) != 0;
    }

    /// <summary>Quits without asking again.</summary>
    public static void TerminateNow()
    {
        if (TryGetExport("appkit_terminate_now", out var terminate))
        {
            ((delegate* unmanaged<void>)terminate)();
        }
    }

    /// <summary>Shows or clears the unsaved-changes dot in the window's close button.</summary>
    public static void SetDocumentEdited(bool edited)
    {
        if (TryGetExport("appkit_set_document_edited", out var setEdited))
        {
            ((delegate* unmanaged<int, void>)setEdited)(edited ? 1 : 0);
        }
    }

    /// <summary>Opens a Finder window with the item selected.</summary>
    public static bool RevealInFinder(string path)
    {
        if (!TryGetExport("appkit_reveal_in_finder", out var reveal))
        {
            return false;
        }

        var utf8 = System.Text.Encoding.UTF8.GetBytes(path + "\0");
        fixed (byte* pointer = utf8)
        {
            ((delegate* unmanaged<byte*, void>)reveal)(pointer);
        }

        return true;
    }

    [UnmanagedCallersOnly]
    private static int OnKeyEvent(int kind, int keyCode, nuint modifierFlags, int isRepeat, int character)
    {
        try
        {
            var keyEvent = new AppKitKeyEvent((AppKitKeyEventKind)kind, (ushort)keyCode, modifierFlags, isRepeat != 0, (char)character);
            return _keyEvent?.Invoke(keyEvent) == true ? 1 : 0;
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit's event loop; let the key through.
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void OnMouseEvent(int kind, int button, double x, double y, double width, double height, nuint modifierFlags, int clickCount)
    {
        try
        {
            var mouseEvent = new AppKitMouseEvent((AppKitMouseEventKind)kind, button, x, y, width, height, modifierFlags, clickCount);
            if (mouseEvent.Kind == AppKitMouseEventKind.TitleBandMoved)
            {
                TitleBandPointerMoved?.Invoke(mouseEvent);
                return;
            }

            _mouseEvent?.Invoke(mouseEvent);
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit's event loop.
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWindowEvent(int kind)
    {
        try
        {
            _windowEvent?.Invoke((AppKitWindowEvent)kind);
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit's notification dispatch.
        }
    }

    [UnmanagedCallersOnly]
    private static int ShouldTerminate() => AskCanClose(AppCloseRequest.Quit);

    [UnmanagedCallersOnly]
    private static int ShouldCloseWindow(nint windowNumber)
    {
        _ = windowNumber;
        return AskCanClose(AppCloseRequest.CloseWindow);
    }

    private static int AskCanClose(AppCloseRequest request)
    {
        try
        {
            return _canClose?.Invoke(request) ?? true ? 1 : 0;
        }
        catch (Exception)
        {
            // Never block quitting because the check itself failed.
            return 1;
        }
    }

    private static bool TryGetExport(string name, out IntPtr export)
    {
        export = IntPtr.Zero;
        return Library.Value != IntPtr.Zero && NativeLibrary.TryGetExport(Library.Value, name, out export);
    }

    private static IntPtr Load()
    {
        var binary = Path.Combine(NSBundle.MainBundle.BundlePath, "Contents", "PlugIns", "AppKitBridge.bundle", "Contents", "MacOS", "AppKitBridge");
        return File.Exists(binary) && NativeLibrary.TryLoad(binary, out var handle) ? handle : IntPtr.Zero;
    }
}
