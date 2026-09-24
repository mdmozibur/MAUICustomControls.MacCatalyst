// AppKit services a Mac Catalyst app cannot reach through UIKit. Built as a macOS plug-in bundle
// (build_bundle.sh), loaded into the app at run time and called through its C functions
// (Platforms/MacCatalyst/AppKitBridge.cs).
//
// Quit and window close: UIKit offers no way to veto either, which a document app needs for
// "Save changes before closing?". The public AppKit entry points they go through,
// -[NSApplication terminate:] (Quit menu, Cmd-Q, Dock, logout) and -[NSWindow performClose:]
// (close button, Cmd-W), are wrapped to ask the app first. When the app says no, it shows its own
// prompt and later calls appkit_terminate_now().

#import <AppKit/AppKit.h>
#import <objc/runtime.h>

typedef int (*AKBShouldTerminate)(void);
typedef int (*AKBShouldCloseWindow)(long windowNumber);

static AKBShouldTerminate gShouldTerminate;
static AKBShouldCloseWindow gShouldCloseWindow;
static IMP gOriginalTerminate;
static IMP gOriginalPerformClose;
static BOOL gBypass;

static void AKBTerminate(id self, SEL _cmd, id sender)
{
    if (!gBypass && gShouldTerminate != NULL && gShouldTerminate() == 0)
    {
        return;
    }

    ((void (*)(id, SEL, id))gOriginalTerminate)(self, _cmd, sender);
}

static void AKBPerformClose(id self, SEL _cmd, id sender)
{
    if (!gBypass && gShouldCloseWindow != NULL && gShouldCloseWindow([(NSWindow *)self windowNumber]) == 0)
    {
        return;
    }

    ((void (*)(id, SEL, id))gOriginalPerformClose)(self, _cmd, sender);
}

// Wraps `selector` on `cls` (adding an override when the method is inherited) and returns the
// implementation it replaced.
static IMP AKBWrap(Class cls, SEL selector, IMP replacement)
{
    Method method = class_getInstanceMethod(cls, selector);
    if (method == NULL)
    {
        return NULL;
    }

    IMP original = method_getImplementation(method);
    class_replaceMethod(cls, selector, replacement, method_getTypeEncoding(method));
    return original;
}

__attribute__((visibility("default")))
int appkit_install_close_guards(AKBShouldTerminate shouldTerminate, AKBShouldCloseWindow shouldCloseWindow)
{
    __block int installed = 0;
    dispatch_block_t install = ^{
        if (gOriginalTerminate != NULL || NSApp == nil)
        {
            installed = gOriginalTerminate != NULL;
            return;
        }

        gShouldTerminate = shouldTerminate;
        gShouldCloseWindow = shouldCloseWindow;
        gOriginalTerminate = AKBWrap(object_getClass(NSApp), @selector(terminate:), (IMP)AKBTerminate);

        // Catalyst windows are private NSWindow subclasses: wrap each class in use and NSWindow itself.
        NSMutableSet<Class> *windowClasses = [NSMutableSet setWithObject:[NSWindow class]];
        for (NSWindow *window in NSApp.windows)
        {
            [windowClasses addObject:object_getClass(window)];
        }

        IMP original = NULL;
        for (Class cls in windowClasses)
        {
            IMP replaced = AKBWrap(cls, @selector(performClose:), (IMP)AKBPerformClose);
            if (original == NULL && replaced != (IMP)AKBPerformClose)
            {
                original = replaced;
            }
        }

        gOriginalPerformClose = original;
        installed = gOriginalTerminate != NULL && gOriginalPerformClose != NULL;
    };

    if ([NSThread isMainThread])
    {
        install();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), install);
    }

    return installed;
}

__attribute__((visibility("default")))
void appkit_terminate_now(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        gBypass = YES;
        [NSApp terminate:nil];
        gBypass = NO;
    });
}

// The dot in the close button that tells the user a window has unsaved changes.
__attribute__((visibility("default")))
void appkit_set_document_edited(int edited)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        for (NSWindow *window in NSApp.windows)
        {
            window.documentEdited = edited != 0;
        }
    });
}

// Finder window with the items selected ("Show in Folder").
__attribute__((visibility("default")))
void appkit_reveal_in_finder(const char *path)
{
    NSURL *url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSWorkspace sharedWorkspace] activateFileViewerSelectingURLs:@[url]];
    });
}

// Keyboard: on Mac Catalyst the keys a focused UIKit text field consumes go straight to the text
// input system and never pass through UIApplication.sendEvent: or the field's pressesBegan:. A local
// event monitor sees every key of the app's windows before any of that, which is what a UWP-style
// key pipeline (accelerators, then KeyDown on the focused element) needs. The callback returns
// nonzero to swallow the event. kind: 0 key down, 1 key up, 2 modifier flags changed.
typedef int (*AKBKeyEvent)(int kind, int keyCode, unsigned long modifierFlags, int isRepeat, int character);

static AKBKeyEvent gKeyEvent;
static id gKeyMonitor;
static Class gUIKitWindowClass;

__attribute__((visibility("default")))
int appkit_install_key_monitor(AKBKeyEvent keyEvent)
{
    __block int installed = 0;
    dispatch_block_t install = ^{
        if (gKeyMonitor != nil)
        {
            installed = 1;
            return;
        }

        gKeyEvent = keyEvent;
        gUIKitWindowClass = NSClassFromString(@"UINSWindow");
        NSEventMask mask = NSEventMaskKeyDown | NSEventMaskKeyUp | NSEventMaskFlagsChanged;
        gKeyMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask handler:^NSEvent *(NSEvent *event) {
            // Only the app's UIKit windows: AppKit panels (alerts, Open/Save) keep their own keys.
            // (UIKit's windows are UINSWindow, possibly behind a KVO subclass.)
            if (event.window == nil || gUIKitWindowClass == Nil || ![event.window isKindOfClass:gUIKitWindowClass])
            {
                return event;
            }

            int kind = event.type == NSEventTypeKeyDown ? 0 : event.type == NSEventTypeKeyUp ? 1 : 2;
            int character = 0;
            if (kind != 2)
            {
                NSString *characters = event.charactersIgnoringModifiers;
                if (characters.length == 1)
                {
                    character = [characters characterAtIndex:0];
                }
            }

            int isRepeat = kind == 0 && event.isARepeat ? 1 : 0;
            return gKeyEvent(kind, event.keyCode, (unsigned long)event.modifierFlags, isRepeat, character) != 0 ? nil : event;
        }];
        installed = gKeyMonitor != nil;
    };

    if ([NSThread isMainThread])
    {
        install();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), install);
    }

    return installed;
}

// Mouse: UIKit on Mac Catalyst turns only the primary button into touches; right and other
// (middle, side) buttons never reach a UIKit view. A local monitor forwards those presses, drags and
// releases, with the location in the window's content view (top-left origin, AppKit points) and
// that view's size, so the app can hit-test them in its UIKit window. The event is never swallowed:
// AppKit and UIKit still handle it (context menus, for one). kind: 0 down, 1 dragged, 2 up.
typedef void (*AKBMouseEvent)(int kind, int button, double x, double y, double width, double height, unsigned long modifierFlags, int clickCount);

static AKBMouseEvent gMouseEvent;
static id gMouseMonitor;
static NSCursor *gCursor;
static NSUInteger gReportedButtons;
static BOOL gPointerInTitleBand;
static NSPoint gLastPoint;
static NSSize gLastSize;
static id gMenuObserver;

// Reports the release of every button reported down (at the last reported location).
static void AKBReleaseReportedButtons(unsigned long modifierFlags)
{
    for (int button = 0; gReportedButtons != 0 && button < 32; button++)
    {
        if ((gReportedButtons & ((NSUInteger)1 << button)) != 0)
        {
            gReportedButtons &= ~((NSUInteger)1 << button);
            gMouseEvent(2, button, gLastPoint.x, gLastPoint.y, gLastSize.width, gLastSize.height, modifierFlags, 0);
        }
    }
}

static BOOL AKBIsUIKitWindow(NSWindow *window)
{
    if (gUIKitWindowClass == Nil)
    {
        gUIKitWindowClass = NSClassFromString(@"UINSWindow");
    }

    return window != nil && gUIKitWindowClass != Nil && [window isKindOfClass:gUIKitWindowClass];
}

// UIKit sets the arrow (or its own pointer style) as the pointer moves over its views, so a cursor
// the app chose is set again for every move: before the event is dispatched, and once more after
// UIKit has handled it.
static void AKBApplyCursor(void)
{
    NSCursor *cursor = gCursor;
    if (cursor == nil)
    {
        return;
    }

    [cursor set];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gCursor == cursor)
        {
            [cursor set];
        }
    });
}

__attribute__((visibility("default")))
int appkit_install_mouse_monitor(AKBMouseEvent mouseEvent)
{
    __block int installed = 0;
    dispatch_block_t install = ^{
        if (gMouseMonitor != nil)
        {
            installed = 1;
            return;
        }

        gMouseEvent = mouseEvent;
        NSEventMask mask = NSEventMaskRightMouseDown | NSEventMaskRightMouseDragged | NSEventMaskRightMouseUp |
            NSEventMaskOtherMouseDown | NSEventMaskOtherMouseDragged | NSEventMaskOtherMouseUp |
            NSEventMaskMouseMoved | NSEventMaskLeftMouseDown | NSEventMaskLeftMouseDragged | NSEventMaskLeftMouseUp |
            NSEventMaskMouseEntered | NSEventMaskMouseExited | NSEventMaskScrollWheel;
        gMouseMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask handler:^NSEvent *(NSEvent *event) {
            if (!AKBIsUIKitWindow(event.window))
            {
                return event;
            }

            AKBApplyCursor();

            NSView *contentView = event.window.contentView;
            if (contentView == nil || gMouseEvent == NULL)
            {
                return event;
            }

            NSPoint point = [contentView convertPoint:event.locationInWindow fromView:nil];
            NSSize size = contentView.bounds.size;
            double y = contentView.isFlipped ? point.y : size.height - point.y;

            // A nested tracking loop (a context menu, for one) can consume a button's release before
            // any monitor sees it. Report the release of every button reported down that is no
            // longer pressed, so the app never keeps a button down forever.
            gLastPoint = NSMakePoint(point.x, y);
            gLastSize = size;
            NSUInteger released = gReportedButtons & ~[NSEvent pressedMouseButtons];
            if (event.type == NSEventTypeRightMouseUp || event.type == NSEventTypeOtherMouseUp)
            {
                released &= ~((NSUInteger)1 << event.buttonNumber);
            }

            for (int button = 0; released != 0 && button < 32; button++)
            {
                if ((released & ((NSUInteger)1 << button)) != 0)
                {
                    released &= ~((NSUInteger)1 << button);
                    gReportedButtons &= ~((NSUInteger)1 << button);
                    gMouseEvent(2, button, point.x, y, size.width, size.height, (unsigned long)event.modifierFlags, 0);
                }
            }

            // UIKit sends no hover events while the pointer is in the title bar band of a window
            // whose content extends under it (clicks still reach the content). Report the pointer's
            // moves there, and leaving it (kind 3, at -1,-1), so the app can track hover itself.
            if (event.type == NSEventTypeMouseMoved || event.type == NSEventTypeMouseExited)
            {
                double band = NSHeight(event.window.frame) - NSHeight(event.window.contentLayoutRect);
                // An exit from any tracking area (a traffic light's, say) may still be in the band.
                BOOL inBand = band > 0 && y >= 0 && y < band && point.x >= 0 && point.x < size.width;
                if (inBand || gPointerInTitleBand)
                {
                    gPointerInTitleBand = inBand;
                    gMouseEvent(3, 0, inBand ? point.x : -1, inBand ? y : -1, size.width, size.height, (unsigned long)event.modifierFlags, 0);
                }

                return event;
            }

            int kind;
            switch (event.type)
            {
                case NSEventTypeRightMouseDown:
                case NSEventTypeOtherMouseDown:
                    kind = 0;
                    break;
                case NSEventTypeRightMouseDragged:
                case NSEventTypeOtherMouseDragged:
                    kind = 1;
                    break;
                case NSEventTypeRightMouseUp:
                case NSEventTypeOtherMouseUp:
                    kind = 2;
                    break;
                default:
                    return event;
            }

            if (kind == 0)
            {
                gReportedButtons |= (NSUInteger)1 << event.buttonNumber;
            }
            else if (kind == 2)
            {
                gReportedButtons &= ~((NSUInteger)1 << event.buttonNumber);
            }

            gMouseEvent(kind, (int)event.buttonNumber, point.x, y, size.width, size.height, (unsigned long)event.modifierFlags, (int)event.clickCount);
            return event;
        }];
        // A context menu opened by a right click tracks the mouse in its own loop and keeps the
        // button's release; as in UWP, where a popup takes the pointer, the press ends when it opens.
        gMenuObserver = [[NSNotificationCenter defaultCenter] addObserverForName:NSMenuDidBeginTrackingNotification
                                                                          object:nil
                                                                           queue:nil
                                                                      usingBlock:^(NSNotification *notification) {
            if (gMouseEvent != NULL)
            {
                AKBReleaseReportedButtons((unsigned long)[NSEvent modifierFlags]);
            }
        }];
        installed = gMouseMonitor != nil;
    };

    if ([NSThread isMainThread])
    {
        install();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), install);
    }

    return installed;
}

static NSCursor *AKBHiddenCursor(void)
{
    static NSCursor *hidden;
    if (hidden == nil)
    {
        NSImage *image = [[NSImage alloc] initWithSize:NSMakeSize(1, 1)];
        hidden = [[NSCursor alloc] initWithImage:image hotSpot:NSZeroPoint];
    }

    return hidden;
}

static NSCursor *AKBDiagonalCursor(BOOL northwestSoutheast)
{
    if (@available(macOS 15.0, *))
    {
        return [NSCursor frameResizeCursorFromPosition:northwestSoutheast ? NSCursorFrameResizePositionTopLeft : NSCursorFrameResizePositionTopRight
                                          inDirections:NSCursorFrameResizeDirectionsAll];
    }

    return [NSCursor crosshairCursor];
}

// The pointer cursor for the app's windows: -1 lets UIKit manage it again (the arrow over plain
// views), otherwise one of the shapes below, kept until the next call.
__attribute__((visibility("default")))
void appkit_set_cursor(int shape)
{
    dispatch_block_t apply = ^{
        NSCursor *cursor;
        switch (shape)
        {
            case 0: cursor = [NSCursor arrowCursor]; break;
            case 1: cursor = [NSCursor IBeamCursor]; break;
            case 2: cursor = [NSCursor crosshairCursor]; break;
            case 3: cursor = [NSCursor pointingHandCursor]; break;
            case 4: cursor = [NSCursor openHandCursor]; break;
            case 5: cursor = [NSCursor closedHandCursor]; break;
            case 6: cursor = [NSCursor resizeLeftRightCursor]; break;
            case 7: cursor = [NSCursor resizeUpDownCursor]; break;
            case 8: cursor = AKBDiagonalCursor(YES); break;
            case 9: cursor = AKBDiagonalCursor(NO); break;
            case 10: cursor = [NSCursor operationNotAllowedCursor]; break;
            case 11: cursor = [NSCursor resizeUpCursor]; break;
            case 12: cursor = [NSCursor contextualMenuCursor]; break;
            case 13: cursor = AKBHiddenCursor(); break;
            default: cursor = nil; break;
        }

        gCursor = cursor;
        [(cursor ?: [NSCursor arrowCursor]) set];
    };

    if ([NSThread isMainThread])
    {
        apply();
    }
    else
    {
        dispatch_async(dispatch_get_main_queue(), apply);
    }
}

// The accent colour the user picked in System Settings > Appearance, in sRGB (0-1 components).
// UIKit on Mac Catalyst does not expose it: an iPad-idiom app's tint stays system blue. Resolved
// under the aqua (light) appearance, the base Windows derives its accent shades from. Returns 0
// when it cannot be converted.
__attribute__((visibility("default")))
int appkit_get_accent_color(double *red, double *green, double *blue)
{
    __block NSColor *color = nil;
    dispatch_block_t read = ^{
        NSAppearance *aqua = [NSAppearance appearanceNamed:NSAppearanceNameAqua];
        [aqua performAsCurrentDrawingAppearance:^{
            color = [[NSColor controlAccentColor] colorUsingColorSpace:[NSColorSpace sRGBColorSpace]];
        }];
    };

    if ([NSThread isMainThread])
    {
        read();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), read);
    }

    if (color == nil)
    {
        return 0;
    }

    *red = color.redComponent;
    *green = color.greenComponent;
    *blue = color.blueComponent;
    return 1;
}

// Window chrome: full screen, the title bar band and the screen a window is on. UIKit on Mac Catalyst
// has no full-screen API (toggleFullScreen: is an NSWindow action) and does not say where the
// traffic-light buttons are once the title is hidden and the content extends under the title bar.

// The app's main UIKit window: the key window when it is one of UIKit's, else the first visible one.
static NSWindow *AKBMainWindow(void)
{
    NSWindow *key = NSApp.keyWindow;
    if (AKBIsUIKitWindow(key))
    {
        return key;
    }

    NSWindow *main = NSApp.mainWindow;
    if (AKBIsUIKitWindow(main))
    {
        return main;
    }

    for (NSWindow *window in NSApp.windows)
    {
        if (AKBIsUIKitWindow(window) && window.isVisible)
        {
            return window;
        }
    }

    return nil;
}

static BOOL AKBIsFullScreen(NSWindow *window)
{
    return window != nil && (window.styleMask & NSWindowStyleMaskFullScreen) != 0;
}

// Enters (enter != 0) or leaves full screen. Returns 1 when the window is already in that state or
// the transition was started, 0 when there is no window or it cannot go full screen.
__attribute__((visibility("default")))
int appkit_set_full_screen(int enter)
{
    __block int result = 0;
    dispatch_block_t apply = ^{
        NSWindow *window = AKBMainWindow();
        if (window == nil)
        {
            return;
        }

        if (AKBIsFullScreen(window) == (enter != 0))
        {
            result = 1;
            return;
        }

        if (enter != 0 && (window.collectionBehavior & NSWindowCollectionBehaviorFullScreenNone) != 0)
        {
            return;
        }

        [window toggleFullScreen:nil];
        result = 1;
    };

    if ([NSThread isMainThread])
    {
        apply();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), apply);
    }

    return result;
}

// The main window's chrome, in AppKit points: the title bar band's height (the part of the frame
// outside contentLayoutRect), the width the traffic-light buttons take from the leading edge
// (their trailing edge plus the same margin again), whether the window is in full screen, and the
// visible frame (without menu bar and Dock) of the screen it is on, with a top-left origin at the
// top-left of the primary screen (UIKit's system coordinates). Returns 0 when there is no window.
__attribute__((visibility("default")))
int appkit_get_window_chrome(double *titleBarHeight, double *leadingInset, int *fullScreen,
                             double *screenX, double *screenY, double *screenWidth, double *screenHeight)
{
    __block int found = 0;
    __block double height = 0, inset = 0, sx = 0, sy = 0, sw = 0, sh = 0;
    __block int isFullScreen = 0;
    dispatch_block_t read = ^{
        NSWindow *window = AKBMainWindow();
        if (window == nil)
        {
            return;
        }

        found = 1;
        isFullScreen = AKBIsFullScreen(window) ? 1 : 0;
        height = NSHeight(window.frame) - NSHeight(window.contentLayoutRect);
        if (height < 0)
        {
            height = 0;
        }

        // In full screen the band (and the buttons in it) only slides in over the content on demand.
        NSButton *close = [window standardWindowButton:NSWindowCloseButton];
        NSButton *zoom = [window standardWindowButton:NSWindowZoomButton];
        if (!isFullScreen && close != nil && zoom != nil && !close.isHidden && close.superview != nil && zoom.superview != nil)
        {
            NSRect closeFrame = [close convertRect:close.bounds toView:nil];
            NSRect zoomFrame = [zoom convertRect:zoom.bounds toView:nil];
            BOOL rightToLeft = window.windowTitlebarLayoutDirection == NSUserInterfaceLayoutDirectionRightToLeft;
            inset = rightToLeft
                ? (NSWidth(window.frame) - NSMinX(zoomFrame)) + (NSWidth(window.frame) - NSMaxX(closeFrame))
                : NSMaxX(zoomFrame) + NSMinX(closeFrame);
        }

        NSScreen *screen = window.screen ?: NSScreen.mainScreen;
        NSScreen *primary = NSScreen.screens.firstObject;
        if (screen != nil && primary != nil)
        {
            NSRect visible = screen.visibleFrame;
            sx = NSMinX(visible);
            sy = NSMaxY(primary.frame) - NSMaxY(visible);
            sw = NSWidth(visible);
            sh = NSHeight(visible);
        }
    };

    if ([NSThread isMainThread])
    {
        read();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), read);
    }

    *titleBarHeight = height;
    *leadingInset = inset;
    *fullScreen = isFullScreen;
    *screenX = sx;
    *screenY = sy;
    *screenWidth = sw;
    *screenHeight = sh;
    return found;
}

// The CoreGraphics display ID of the screen the main window is on (the main screen when there is
// no window). Returns 0 when there is no screen at all.
__attribute__((visibility("default")))
int appkit_get_window_display(uint32_t *displayId)
{
    __block uint32_t result = 0;
    dispatch_block_t read = ^{
        NSWindow *window = AKBMainWindow();
        NSScreen *screen = window.screen ?: NSScreen.mainScreen;
        NSNumber *number = screen.deviceDescription[@"NSScreenNumber"];
        result = number != nil ? number.unsignedIntValue : 0;
    };

    if ([NSThread isMainThread])
    {
        read();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), read);
    }

    *displayId = result;
    return result != 0;
}

// Reports full-screen transitions of the app's UIKit windows. kind: 0 will enter, 1 did enter,
// 2 will exit, 3 did exit, 4 the window moved to another screen.
typedef void (*AKBWindowEvent)(int kind);

static AKBWindowEvent gWindowEvent;
static NSMutableArray *gWindowObservers;

__attribute__((visibility("default")))
int appkit_install_window_observer(AKBWindowEvent windowEvent)
{
    __block int installed = 0;
    dispatch_block_t install = ^{
        if (gWindowObservers != nil)
        {
            installed = 1;
            return;
        }

        gWindowEvent = windowEvent;
        gWindowObservers = [NSMutableArray array];
        NSDictionary<NSNotificationName, NSNumber *> *events = @{
            NSWindowWillEnterFullScreenNotification: @0,
            NSWindowDidEnterFullScreenNotification: @1,
            NSWindowWillExitFullScreenNotification: @2,
            NSWindowDidExitFullScreenNotification: @3,
            NSWindowDidChangeScreenNotification: @4,
        };
        for (NSNotificationName name in events)
        {
            int kind = events[name].intValue;
            id observer = [[NSNotificationCenter defaultCenter] addObserverForName:name
                                                                            object:nil
                                                                             queue:nil
                                                                        usingBlock:^(NSNotification *notification) {
                if (gWindowEvent != NULL && AKBIsUIKitWindow(notification.object))
                {
                    gWindowEvent(kind);
                }
            }];
            [gWindowObservers addObject:observer];
        }

        installed = 1;
    };

    if ([NSThread isMainThread])
    {
        install();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), install);
    }

    return installed;
}

// The visible frame (no menu bar or Dock) of the screen that holds most of the given rectangle, or of
// the main screen when it is on none, in UIKit system coordinates (top-left origin at the primary
// screen's top-left). Returns 1 when the rectangle overlaps a screen, 0 when it fell back.
__attribute__((visibility("default")))
int appkit_get_visible_frame(double x, double y, double width, double height,
                             double *screenX, double *screenY, double *screenWidth, double *screenHeight)
{
    __block int overlaps = 0;
    __block NSRect result = NSZeroRect;
    dispatch_block_t read = ^{
        NSScreen *primary = NSScreen.screens.firstObject;
        if (primary == nil)
        {
            return;
        }

        CGFloat top = NSMaxY(primary.frame);
        NSRect rect = NSMakeRect(x, top - y - height, width, height);
        NSScreen *best = nil;
        CGFloat bestArea = 0;
        for (NSScreen *screen in NSScreen.screens)
        {
            NSRect overlap = NSIntersectionRect(rect, screen.frame);
            CGFloat area = NSWidth(overlap) * NSHeight(overlap);
            if (area > bestArea)
            {
                bestArea = area;
                best = screen;
            }
        }

        overlaps = best != nil;
        NSRect visible = (best ?: NSScreen.mainScreen ?: primary).visibleFrame;
        result = NSMakeRect(NSMinX(visible), top - NSMaxY(visible), NSWidth(visible), NSHeight(visible));
    };

    if ([NSThread isMainThread])
    {
        read();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), read);
    }

    *screenX = NSMinX(result);
    *screenY = NSMinY(result);
    *screenWidth = NSWidth(result);
    *screenHeight = NSHeight(result);
    return overlaps;
}
