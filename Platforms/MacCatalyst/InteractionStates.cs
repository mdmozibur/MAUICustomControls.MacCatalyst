using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

using Controls = MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// The pointer-over and pressed backgrounds of UWP's button-like templates, for controls drawn by a
/// native handler.
/// </summary>
/// <remarks>
/// A UWP template's PointerOver and Pressed states paint its root with a theme brush
/// (ButtonBackgroundPointerOver / ButtonBackgroundPressed for ToggleButtonWithIcon, ToggleDropdown and
/// SegmentedButtonItem; ToggleButtonBackground* for ToggleButton). The colour is read from the app's
/// resources each time a state is drawn, so it follows the app's light/dark theme dictionaries and any
/// override the app defines; without the resource it falls back to WinUI 2's own value.
/// </remarks>
internal static class InteractionColors
{
    public const string ButtonPointerOverKey = "ButtonBackgroundPointerOver";
    public const string ButtonPressedKey = "ButtonBackgroundPressed";
    public const string ToggleButtonPointerOverKey = "ToggleButtonBackgroundPointerOver";
    public const string ToggleButtonPressedKey = "ToggleButtonBackgroundPressed";

    // WinUI 2 ControlFillColorSecondary / ControlFillColorTertiary.
    private static readonly UIColor PointerOverFallback = Dynamic(0x80F9F9F9, 0x15FFFFFF);
    private static readonly UIColor PressedFallback = Dynamic(0x4DF9F9F9, 0x08FFFFFF);

    public static UIColor PointerOver(string key = ButtonPointerOverKey) => Resolve(key, ButtonPointerOverKey, PointerOverFallback);

    public static UIColor Pressed(string key = ButtonPressedKey) => Resolve(key, ButtonPressedKey, PressedFallback);

    /// <summary>The app's brush or colour resource <paramref name="key"/>, else <paramref name="fallback"/>.</summary>
    public static UIColor Get(string key, UIColor fallback) => TryResolve(key) ?? fallback;

    /// <summary>A light/dark colour from ARGB values, for WinUI fallbacks.</summary>
    public static UIColor LightDark(uint light, uint dark) => Dynamic(light, dark);

    private static UIColor Resolve(string key, string fallbackKey, UIColor fallback)
    {
        return TryResolve(key) ?? (key == fallbackKey ? null : TryResolve(fallbackKey)) ?? fallback;
    }

    private static UIColor? TryResolve(string key)
    {
        if (Application.Current?.Resources is not { } resources || !resources.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            SolidColorBrush { Color: { } color } => color.ToPlatform(),
            Color color => color.ToPlatform(),
            _ => null,
        };
    }

    private static UIColor Dynamic(uint light, uint dark)
    {
        return new UIColor(traits => FromArgb(traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? dark : light));

        static UIColor FromArgb(uint argb) => UIColor.FromRGBA(
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb,
            (byte)(argb >> 24));
    }
}

/// <summary>
/// Whether the pointer is over a native view: UWP's PointerOver state, which UIControl has no state
/// for. A UIHoverGestureRecognizer reports it without taking part in the view's clicks.
/// </summary>
internal sealed class PointerHoverTracker : IDisposable
{
    private readonly UIHoverGestureRecognizer _recognizer;
    private readonly Action _changed;
    private readonly bool _tracksLocation;
    private UIView? _view;

    /// <param name="tracksLocation">Also report moves while hovered (to tell which part is under the pointer).</param>
    public PointerHoverTracker(UIView view, Action changed, bool tracksLocation = false)
    {
        _view = view;
        _changed = changed;
        _tracksLocation = tracksLocation;
        _recognizer = new UIHoverGestureRecognizer(OnHover)
        {
            CancelsTouchesInView = false,
            ShouldRecognizeSimultaneously = static (_, _) => true,
        };
        view.AddGestureRecognizer(_recognizer);
        TitleBandHover.Register(this);
    }

    public bool IsHovered { get; private set; }

    internal UIView? View => _view;

    // Hover reported by the recognizer, and inside the title bar band, where the recognizer is
    // never told (see TitleBandHover).
    private bool _recognizerHovered;
    private bool _bandHovered;

    internal void SetTitleBandHover(bool hovered, CoreGraphics.CGPoint windowPoint)
    {
        if (_view is null || (hovered == _bandHovered && !(hovered && _tracksLocation)))
        {
            return;
        }

        _bandHovered = hovered;
        Update(hovered && _tracksLocation ? _view.ConvertPointFromView(windowPoint, null) : Location);
    }

    /// <summary>The pointer's position in the view while it is over it.</summary>
    public CoreGraphics.CGPoint Location { get; private set; }

    private void OnHover(UIHoverGestureRecognizer recognizer)
    {
        _recognizerHovered = recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed;
        var location = _recognizerHovered && _tracksLocation && _view is not null ? recognizer.LocationInView(_view) : default;
        Update(location);
    }

    private void Update(CoreGraphics.CGPoint location)
    {
        var hovered = _recognizerHovered || _bandHovered;
        if (!hovered)
        {
            location = default;
        }

        if (hovered == IsHovered && location == Location)
        {
            return;
        }

        IsHovered = hovered;
        Location = location;
        _changed();
    }

    public void Dispose()
    {
        _view?.RemoveGestureRecognizer(_recognizer);
        _recognizer.Dispose();
        _view = null;
        IsHovered = _recognizerHovered = _bandHovered = false;
    }
}

/// <summary>
/// Hover inside the title bar band. A window whose content extends under the title bar (UWP's
/// ExtendViewIntoTitleBar) still gets clicks there, but UIKit sends no hover events in that band, so
/// buttons in a UWP title bar never showed PointerOver. The AppKit plug-in reports the pointer's
/// moves in the band (<see cref="AppKitBridge.TitleBandPointerMoved"/>); the view under the pointer
/// is found by hit-testing, and then:
/// <list type="bullet">
/// <item>native handlers' <see cref="PointerHoverTracker"/>s whose view contains it are hovered;</item>
/// <item>MAUI elements under it with a PointerOver visual state (a Button's implicit style) go to
/// that state, and back to Normal (or Disabled) when the pointer leaves;</item>
/// <item><see cref="Controls.ITitleBandHoverTarget"/>s under it are told.</item>
/// </list>
/// </summary>
internal static class TitleBandHover
{
    private const string PointerOverStateName = "PointerOver";

    private static readonly List<WeakReference<PointerHoverTracker>> Trackers = new();
    private static readonly List<WeakReference<VisualElement>> HoveredElements = new();
    private static bool _subscribed;
    private static UIView? _lastHit;
    private static List<VisualElement> _lastElements = [];

    public static void Register(PointerHoverTracker tracker)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            AppKitBridge.TitleBandPointerMoved += OnTitleBandPointerMoved;
        }

        Trackers.RemoveAll(reference => !reference.TryGetTarget(out _));
        Trackers.Add(new WeakReference<PointerHoverTracker>(tracker));
    }

    private static void OnTitleBandPointerMoved(AppKitMouseEvent mouseEvent)
    {
        UIView? hit = null;
        var point = CoreGraphics.CGPoint.Empty;
        if (mouseEvent.X >= 0 && mouseEvent.ContentWidth > 0 && mouseEvent.ContentHeight > 0 && FindWindow() is { } window)
        {
            point = new CoreGraphics.CGPoint(
                mouseEvent.X * window.Bounds.Width / mouseEvent.ContentWidth,
                mouseEvent.Y * window.Bounds.Height / mouseEvent.ContentHeight);
            hit = window.HitTest(point, null);
        }

        foreach (var reference in Trackers.ToArray())
        {
            if (reference.TryGetTarget(out var tracker) && tracker.View is { } view)
            {
                tracker.SetTitleBandHover(hit is not null && hit.IsDescendantOfView(view), point);
            }
        }

        // The elements are looked up when the pointer reaches another view; on other moves they are
        // only put back into PointerOver (a click resets a button's visual state).
        if (!ReferenceEquals(hit, _lastHit))
        {
            _lastHit = hit;
            _lastElements = hit is null ? [] : ElementsUnder(hit);
        }

        if (_lastElements.Count > 0 || HoveredElements.Count > 0)
        {
            UpdateElements(_lastElements);
        }
    }

    private static void UpdateElements(IReadOnlyList<VisualElement> elements)
    {
        foreach (var reference in HoveredElements)
        {
            if (reference.TryGetTarget(out var previous) && !elements.Contains(previous))
            {
                SetPointerOver(previous, false);
            }
        }

        HoveredElements.Clear();
        foreach (var element in elements)
        {
            SetPointerOver(element, true);
            HoveredElements.Add(new WeakReference<VisualElement>(element));
        }
    }

    private static void SetPointerOver(VisualElement element, bool isPointerOver)
    {
        if (element is Controls.ITitleBandHoverTarget target)
        {
            target.SetTitleBandPointerOver(isPointerOver);
            return;
        }

        var current = VisualStateManager.GetVisualStateGroups(element)
            .FirstOrDefault(group => group.States.Any(state => state.Name == PointerOverStateName))?.CurrentState?.Name;
        if (isPointerOver)
        {
            // Pressed and Disabled win over PointerOver, as in the element's own state logic.
            if (element.IsEnabled && current is null or "Normal")
            {
                VisualStateManager.GoToState(element, PointerOverStateName);
            }
        }
        else if (current == PointerOverStateName)
        {
            VisualStateManager.GoToState(element, element.IsEnabled ? "Normal" : "Disabled");
        }
    }

    // The MAUI elements under the pointer that show a pointer-over state, innermost first.
    private static List<VisualElement> ElementsUnder(UIView hit)
    {
        var result = new List<VisualElement>();
        var element = FindElement(hit);
        for (Element? current = element; current is not null and not Page; current = current.Parent)
        {
            if (current is VisualElement visual && visual.IsVisible &&
                (visual is Controls.ITitleBandHoverTarget ||
                 VisualStateManager.GetVisualStateGroups(visual).Any(group => group.States.Any(state => state.Name == PointerOverStateName))))
            {
                result.Add(visual);
            }
        }

        return result;
    }

    // The innermost MAUI element whose platform (or container) view is the hit view or one of its
    // superviews. MAUI keeps no map from a native view to its element, so the window's page is
    // searched each time the pointer reaches another view.
    private static VisualElement? FindElement(UIView hit)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not IVisualTreeElement page)
        {
            return null;
        }

        var byView = new Dictionary<IntPtr, VisualElement>();
        foreach (var descendant in page.GetVisualTreeDescendants().OfType<VisualElement>())
        {
            if (descendant.Handler is IPlatformViewHandler handler)
            {
                if (handler.PlatformView is { } platformView)
                {
                    byView.TryAdd(platformView.Handle, descendant);
                }

                if (handler.ContainerView is { } containerView)
                {
                    byView.TryAdd(containerView.Handle, descendant);
                }
            }
        }

        for (var view = hit; view is not null; view = view.Superview)
        {
            if (byView.TryGetValue(view.Handle, out var element))
            {
                return element;
            }
        }

        return null;
    }

    private static UIWindow? FindWindow()
    {
        var windows = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .ToList();
        return windows.FirstOrDefault(window => window.IsKeyWindow) ?? windows.FirstOrDefault();
    }
}

/// <summary>
/// UWP's Button template paints its PointerOver and Pressed fills over whatever Background the button
/// has. The converted implicit Button style puts those fills in PointerOver/Pressed visual states,
/// but a state's setter cannot replace a Background set on the button itself (measured: a button
/// with Background="Transparent" stays transparent in PointerOver), so every such button (a title
/// bar's icon buttons, for one) showed no hover. The fill a state would paint is therefore drawn
/// natively while the pointer is over the button (also in the title bar band, see
/// <see cref="PointerHoverTracker"/>) or it is pressed, taken from the button's own PointerOver /
/// Pressed state; a button whose states set no Background keeps its own look.
/// </summary>
internal static class ButtonInteractionStates
{
    private const string AttachKey = "UwpButtonInteractionStates";

    private static readonly ConditionalWeakTable<UIButton, PointerHoverTracker> Trackers = new();

    public static void Configure()
    {
        ButtonHandler.Mapper.AppendToMapping(AttachKey, (handler, _) => Attach(handler));
        // After the Background mapping (MacIdiomControlStyle's), so the state fill wins while it lasts.
        ButtonHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, view) => Apply(handler, view));
    }

    private static void Attach(IButtonHandler handler)
    {
        if (handler.PlatformView is not { } button || Trackers.TryGetValue(button, out _))
        {
            return;
        }

        var weakHandler = new WeakReference<IButtonHandler>(handler);
        Trackers.Add(button, new PointerHoverTracker(button, () => Refresh(weakHandler)));
        if (handler.VirtualView is Button virtualButton)
        {
            virtualButton.Pressed += (_, _) => Refresh(weakHandler);
            virtualButton.Released += (_, _) => Refresh(weakHandler);
        }
    }

    private static void Refresh(WeakReference<IButtonHandler> weakHandler)
    {
        if (weakHandler.TryGetTarget(out var handler) && handler.VirtualView is not null)
        {
            // MAUI draws the button's own Background again; Apply then adds the state's fill.
            handler.UpdateValue(nameof(IView.Background));
        }
    }

    private static void Apply(IButtonHandler handler, IButton view)
    {
        if (handler.PlatformView is not { } button || view is not Button element || !element.IsEnabled ||
            !Trackers.TryGetValue(button, out var tracker))
        {
            return;
        }

        var stateName = element.IsPressed ? "Pressed" : tracker.IsHovered ? "PointerOver" : null;
        if (stateName is not null && StateBackground(element, stateName) is { } color)
        {
            button.BackgroundColor = color;
        }
    }

    // The Background (or BackgroundColor) the element's own visual state sets, resolved.
    private static UIColor? StateBackground(VisualElement element, string stateName)
    {
        var state = VisualStateManager.GetVisualStateGroups(element)
            .Select(group => group.States.FirstOrDefault(candidate => candidate.Name == stateName))
            .FirstOrDefault(candidate => candidate is not null);
        var setter = state?.Setters.FirstOrDefault(candidate =>
            string.IsNullOrEmpty(candidate.TargetName) &&
            (candidate.Property == VisualElement.BackgroundProperty || candidate.Property == VisualElement.BackgroundColorProperty));
        var value = setter?.Value is Microsoft.Maui.Controls.Internals.DynamicResource resource
            ? FindResource(element, resource.Key)
            : setter?.Value;

        return value switch
        {
            SolidColorBrush { Color: { } color } => color.ToPlatform(),
            Color color => color.ToPlatform(),
            _ => null,
        };
    }

    private static object? FindResource(Element element, string key)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current is VisualElement { Resources: { } resources } && resources.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return Application.Current?.Resources.TryGetValue(key, out var appValue) == true ? appValue : null;
    }
}
