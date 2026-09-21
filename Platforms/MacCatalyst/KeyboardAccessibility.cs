using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>The keys a focused control answers to, as UWP's controls do.</summary>
internal static class KeyboardKeys
{
    /// <summary>Space, Return or the keypad's Enter: activate the focused control.</summary>
    public static bool IsActivation(UIKeyboardHidUsage key) =>
        key is UIKeyboardHidUsage.KeyboardSpacebar or UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKeyboardHidUsage.KeypadEnter;

    public static UIKeyboardHidUsage? KeyOf(NSSet<UIPress> presses, bool withoutModifiers = true)
    {
        foreach (var press in presses.ToArray())
        {
            if (press.Key is { } key && (!withoutModifiers || (key.ModifierFlags & ~UIKeyModifierFlags.NumericPad) == 0))
            {
                return key.KeyCode;
            }
        }

        return null;
    }
}

/// <summary>
/// UIKit accessibility traits the enum does not name: UIAccessibilityTraitToggleButton (a check box
/// or toggle; VoiceOver reads it with its on/off state from the Selected trait).
/// </summary>
internal static class AccessibilityTraitBits
{
    public static readonly UIAccessibilityTrait ToggleButton = Resolve(UIAccessibilityTraits.ToggleButton);

    /// <summary>UIAccessibilityTraitTabBar: a container whose buttons are tabs (Mac: a tab group).</summary>
    public static readonly UIAccessibilityTrait TabBar = Resolve(UIAccessibilityTraits.TabBar);

    private static UIAccessibilityTrait Resolve(UIAccessibilityTraits trait)
    {
        try
        {
            return trait.GetConstantValue() is { } bits
                ? (UIAccessibilityTrait)(long)bits
                : UIAccessibilityTrait.None;
        }
        catch (Exception)
        {
            return UIAccessibilityTrait.None;
        }
    }
}

/// <summary>
/// The native view of an <see cref="IAccessibleItem"/> (a tab header, a radio choice, an expander
/// header): with keyboard navigation on it takes keyboard focus and draws the focus ring, answers
/// Space/Return and the arrow keys while focused, and is one element to VoiceOver with the item's
/// role, name and state. The owning control's items share a focus group, so Tab stops once on the
/// group (on the selected item) and the arrow keys move within it, as in UWP.
/// </summary>
public sealed class AccessibleItemHandler : ContentViewHandler
{
    public static readonly PropertyMapper<IContentView, AccessibleItemHandler> AccessibleItemMapper = new(ContentViewHandler.Mapper)
    {
        // MAUI's semantics mapping writes the label (null without a Description); the item's own
        // label and traits are applied over it.
        [nameof(IView.Semantics)] = (handler, view) =>
        {
            ViewHandler.MapSemantics(handler, view);
            handler.UpdateAccessibility();
        },
        [nameof(IView.IsEnabled)] = (handler, view) =>
        {
            ViewHandler.MapIsEnabled(handler, view);
            handler.UpdateAccessibility();
        },
        [nameof(AccessibleItemView.Role)] = (handler, _) => handler.UpdateAccessibility(),
        [nameof(AccessibleItemView.Name)] = (handler, _) => handler.UpdateAccessibility(),
        [nameof(AccessibleItemView.IsSelected)] = (handler, _) => handler.UpdateAccessibility(),
        [nameof(AccessibleItemView.FocusGroup)] = (handler, _) => handler.UpdateAccessibility(),
        // TabItem's title.
        ["Title"] = (handler, _) => handler.UpdateAccessibility(),
        ["IsClosable"] = (handler, _) => handler.UpdateAccessibility(),
        ["UseSystemFocusVisuals"] = (handler, _) => handler.PlatformView?.SetNeedsLayout(),
    };

    public static readonly CommandMapper<IContentView, AccessibleItemHandler> AccessibleItemCommandMapper = new(ContentViewHandler.CommandMapper)
    {
        [AccessibleItemFocus.FocusCommand] = (handler, _, _) => handler.MoveFocusHere(),
    };

    public AccessibleItemHandler()
        : base(AccessibleItemMapper, AccessibleItemCommandMapper)
    {
    }

    protected override Microsoft.Maui.Platform.ContentView CreatePlatformView() =>
        new AccessibleItemPlatformView { CrossPlatformLayout = VirtualView, Item = VirtualView as IAccessibleItem };

    protected override void ConnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is AccessibleItemPlatformView itemView)
        {
            itemView.Item = VirtualView as IAccessibleItem;
        }

        UpdateAccessibility();
    }

    protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        if (platformView is AccessibleItemPlatformView itemView)
        {
            itemView.Item = null;
            itemView.AccessibilityCustomActions = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void UpdateAccessibility()
    {
        if (PlatformView is not AccessibleItemPlatformView view || VirtualView is not IAccessibleItem item)
        {
            return;
        }

        view.IsAccessibilityElement = true;

        var role = item.AccessibleRole;
        var traits = UIAccessibilityTrait.Button;
        if (role is AccessibleItemRole.Tab or AccessibleItemRole.RadioButton && item.IsItemSelected)
        {
            traits |= UIAccessibilityTrait.Selected;
        }

        if (!item.IsEnabled)
        {
            traits |= UIAccessibilityTrait.NotEnabled;
        }

        if (item.Semantics?.IsHeading == true)
        {
            traits |= UIAccessibilityTrait.Header;
        }

        view.AccessibilityTraits = traits;
        view.AccessibilityLabel = FirstNonEmpty(item.AccessibleName, item.Semantics?.Description, TextOf(item, 6));
        view.AccessibilityHint = item.Semantics?.Hint;

        if (role == AccessibleItemRole.Disclosure)
        {
            if (OperatingSystem.IsMacCatalystVersionAtLeast(18))
            {
                view.AccessibilityExpandedStatus = item.IsItemSelected ? UIAccessibilityExpandedStatus.Expanded : UIAccessibilityExpandedStatus.Collapsed;
            }
            else
            {
                view.AccessibilityValue = item.IsItemSelected ? "expanded" : "collapsed";
            }
        }

        view.AccessibilityCustomActions = item.AccessibleActions is { Count: > 0 } actions
            ? actions.Select(action => new UIAccessibilityCustomAction(action.Name, (Func<UIAccessibilityCustomAction, bool>)(_ =>
            {
                action.Invoke();
                return true;
            }))).ToArray()
            : null;

        view.FocusGroupIdentifier = item.FocusGroup;
        view.MarkTabContainer();
    }

    // After an arrow key selected a sibling: focus follows the selection, but only when focus is on
    // one of the group's items (a click or code must not pull keyboard focus here).
    private void MoveFocusHere()
    {
        if (PlatformView is not { Window: not null } view || UIFocusSystem.Create(view) is not { } focusSystem)
        {
            return;
        }

        if (focusSystem.FocusedItem is AccessibleItemPlatformView focused
            && !ReferenceEquals(focused, view)
            && focused.FocusGroupIdentifier is { } group
            && group == view.FocusGroupIdentifier)
        {
            focusSystem.RequestFocusUpdate(view);
            focusSystem.UpdateFocusIfNeeded();
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    // The first text the item shows: a label's, or a radio button's string content.
    internal static string? TextOf(object element, int depth)
    {
        // Decorative parts (a chevron glyph) are not part of the name.
        if (element is Microsoft.Maui.Controls.BindableObject bindable
            && (Microsoft.Maui.Controls.AutomationProperties.GetExcludedWithChildren(bindable) == true
                || Microsoft.Maui.Controls.AutomationProperties.GetIsInAccessibleTree(bindable) == false))
        {
            return null;
        }

        switch (element)
        {
            case Microsoft.Maui.Controls.Label { Text: { Length: > 0 } text }:
                return text;
            case Microsoft.Maui.Controls.RadioButton { Content: string content } when content.Length > 0:
                return content;
            case Microsoft.Maui.Controls.Button { Text: { Length: > 0 } buttonText }:
                return buttonText;
        }

        if (depth == 0 || element is not IVisualTreeElement tree)
        {
            return null;
        }

        foreach (var child in tree.GetVisualChildren())
        {
            if (child is Microsoft.Maui.Controls.VisualElement { IsVisible: false })
            {
                continue;
            }

            if (TextOf(child, depth - 1) is { } text)
            {
                return text;
            }
        }

        return null;
    }
}

/// <summary>The platform view <see cref="AccessibleItemHandler"/> creates.</summary>
public sealed class AccessibleItemPlatformView : Microsoft.Maui.Platform.ContentView
{
    private readonly HashSet<UIKeyboardHidUsage> _swallowed = new();
    private WeakReference<IAccessibleItem>? _item;

    public IAccessibleItem? Item
    {
        get => _item is not null && _item.TryGetTarget(out var item) ? item : null;
        set => _item = value is null ? null : new WeakReference<IAccessibleItem>(value);
    }

    public override bool CanBecomeFocused =>
        UserInteractionEnabled && !Hidden && Item is { IsEnabled: true, Visibility: Visibility.Visible };

    // Tab into the group lands on the selected tab or choice.
    public override nint FocusGroupPriority
    {
        get => Item is { AccessibleRole: AccessibleItemRole.Tab or AccessibleItemRole.RadioButton, IsItemSelected: true }
            ? (nint)(long)UIFocusGroupPriority.Prioritized
            : base.FocusGroupPriority;
        set => base.FocusGroupPriority = value;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        FocusRing.UpdateCustomView(this, Item as Microsoft.Maui.Controls.BindableObject);
    }

    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        MarkTabContainer();
    }

    // The row a tab sits in is a tab list to VoiceOver (on the Mac a tab group whose tabs are
    // radio-button-like tab buttons), as a segmented control's segments are.
    internal void MarkTabContainer()
    {
        if (Item?.AccessibleRole == AccessibleItemRole.Tab && Superview is { IsAccessibilityElement: false } row
            && AccessibilityTraitBits.TabBar != UIAccessibilityTrait.None)
        {
            row.AccessibilityTraits |= AccessibilityTraitBits.TabBar;
        }
    }

    public override bool AccessibilityActivate()
    {
        if (Item is not { IsEnabled: true } item)
        {
            return false;
        }

        item.Invoke();
        return true;
    }

    // VoiceOver's increment/decrement on an item move within its group, like the arrow keys.
    public override void AccessibilityIncrement() => Item?.Move(1);

    public override void AccessibilityDecrement() => Item?.Move(-1);

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (Focused && Item is { IsEnabled: true } item && KeyboardKeys.KeyOf(presses) is { } key && Handle(item, key))
        {
            _swallowed.Add(key);
            return;
        }

        base.PressesBegan(presses, evt);
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (KeyboardKeys.KeyOf(presses, withoutModifiers: false) is { } key && _swallowed.Remove(key))
        {
            return;
        }

        base.PressesEnded(presses, evt);
    }

    public override void PressesCancelled(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (KeyboardKeys.KeyOf(presses, withoutModifiers: false) is { } key && _swallowed.Remove(key))
        {
            return;
        }

        base.PressesCancelled(presses, evt);
    }

    private static bool Handle(IAccessibleItem item, UIKeyboardHidUsage key)
    {
        if (KeyboardKeys.IsActivation(key))
        {
            item.Invoke();
            return true;
        }

        switch (item.AccessibleRole)
        {
            case AccessibleItemRole.Tab:
                return key switch
                {
                    UIKeyboardHidUsage.KeyboardLeftArrow => item.Move(-1),
                    UIKeyboardHidUsage.KeyboardRightArrow => item.Move(1),
                    _ => false,
                };
            case AccessibleItemRole.RadioButton:
                return key switch
                {
                    UIKeyboardHidUsage.KeyboardLeftArrow or UIKeyboardHidUsage.KeyboardUpArrow => item.Move(-1),
                    UIKeyboardHidUsage.KeyboardRightArrow or UIKeyboardHidUsage.KeyboardDownArrow => item.Move(1),
                    _ => false,
                };
            case AccessibleItemRole.Disclosure:
                // → expands, ← collapses (macOS disclosure triangles); already there, nothing to do.
                if (key == UIKeyboardHidUsage.KeyboardRightArrow || key == UIKeyboardHidUsage.KeyboardLeftArrow)
                {
                    if (item.IsItemSelected != (key == UIKeyboardHidUsage.KeyboardRightArrow))
                    {
                        item.Invoke();
                    }

                    return true;
                }

                return false;
            default:
                return false;
        }
    }
}

/// <summary>
/// VoiceOver for the UIButton-based controls (check box, toggle button, dropdown): a toggle's
/// on/off state, and a name for an icon-only button (its tooltip) when it has neither text nor a
/// SemanticProperties.Description.
/// </summary>
internal static class ButtonAccessibility
{
    public static void Update(UIButton button, Microsoft.Maui.Controls.VisualElement element, bool isToggle, bool isOn, string? text)
    {
        var traits = UIAccessibilityTrait.Button;
        if (isToggle)
        {
            traits |= AccessibilityTraitBits.ToggleButton;
        }

        if (isOn)
        {
            traits |= UIAccessibilityTrait.Selected;
        }

        if (!element.IsEnabled)
        {
            traits |= UIAccessibilityTrait.NotEnabled;
        }

        if (Microsoft.Maui.Controls.SemanticProperties.GetHeadingLevel(element) != Microsoft.Maui.SemanticHeadingLevel.None)
        {
            traits |= UIAccessibilityTrait.Header;
        }

        button.AccessibilityTraits = traits;

        // A toggle's value is its state, "1" or "0" as UISwitch reports it (VoiceOver reads it as
        // checked/unchecked, on/off).
        button.AccessibilityValue = isToggle ? (isOn ? "1" : "0") : null;

        var description = Microsoft.Maui.Controls.SemanticProperties.GetDescription(element);
        button.AccessibilityLabel = !string.IsNullOrWhiteSpace(description)
            ? description
            : !string.IsNullOrWhiteSpace(text)
                ? text
                : Microsoft.Maui.Controls.ToolTipProperties.GetText(element)?.ToString();
        button.AccessibilityHint = Microsoft.Maui.Controls.SemanticProperties.GetHint(element);
    }
}

/// <summary>
/// Names for icon buttons. A converted UWP icon button shows a glyph of an icon font as its text
/// (the app's font maps letters to icons), so VoiceOver read "A" or "@". UWP's automation falls
/// back to the tooltip for a button without a name; so does this: a MAUI Button whose text is one
/// or two characters, or an ImageButton, is named by its tooltip unless it has a
/// SemanticProperties.Description.
/// </summary>
public static class IconButtonNames
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        foreach (var key in new[] { nameof(ITextButton.Text), "ToolTip", nameof(IView.Semantics) })
        {
            ButtonHandler.Mapper.AppendToMapping(key, (handler, view) => Apply(handler.PlatformView, view));
            ImageButtonHandler.Mapper.AppendToMapping(key, (handler, view) => Apply(handler.PlatformView, view));
        }
    }

    private static void Apply(UIView? platformView, IView view)
    {
        if (platformView is null || view is not Microsoft.Maui.Controls.VisualElement element
            || !string.IsNullOrWhiteSpace(Microsoft.Maui.Controls.SemanticProperties.GetDescription(element)))
        {
            return;
        }

        var isIconOnly = element switch
        {
            Microsoft.Maui.Controls.Button button => string.IsNullOrWhiteSpace(button.Text) || button.Text.Trim().Length <= 2,
            Microsoft.Maui.Controls.ImageButton => true,
            _ => false,
        };
        if (isIconOnly && Microsoft.Maui.Controls.ToolTipProperties.GetText(element)?.ToString() is { Length: > 0 } tooltip)
        {
            platformView.AccessibilityLabel = tooltip;
        }
    }
}
