namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>What a keyboard- and VoiceOver-reachable part of a custom control is.</summary>
public enum AccessibleItemRole
{
    /// <summary>A push button: Space or Return invokes it.</summary>
    Button,

    /// <summary>One tab of a tab strip: selected or not; ← and → select the neighbouring tab.</summary>
    Tab,

    /// <summary>One choice of a radio group: selected or not; the arrow keys select the neighbouring choice.</summary>
    RadioButton,

    /// <summary>An expander's header: Space or Return toggles it, → expands and ← collapses it.</summary>
    Disclosure,
}

/// <summary>An extra action VoiceOver offers on an item (its Actions rotor), e.g. closing a tab.</summary>
public sealed record AccessibleItemAction(string Name, Action Invoke);

/// <summary>
/// A part of a custom control (a tab header, a radio choice, an expander header) that the keyboard
/// and VoiceOver reach like the native control UWP draws: it takes keyboard focus (Tab, with
/// keyboard navigation on), is activated with Space or Return, moves between its siblings with the
/// arrow keys, and tells VoiceOver its role, name and state. On Mac Catalyst
/// <c>AccessibleItemHandler</c> gives the platform view these behaviours.
/// </summary>
public interface IAccessibleItem : IView
{
    AccessibleItemRole AccessibleRole { get; }

    /// <summary>The name VoiceOver reads; null falls back to SemanticProperties.Description, then to the item's text.</summary>
    string? AccessibleName { get; }

    /// <summary>Selected (a tab, a radio choice) or expanded (a disclosure).</summary>
    bool IsItemSelected { get; }

    /// <summary>Items sharing a focus group are one stop in the Tab order; the arrow keys move within it.</summary>
    string? FocusGroup { get; }

    IReadOnlyList<AccessibleItemAction>? AccessibleActions => null;

    /// <summary>Space, Return, or VoiceOver's activate.</summary>
    void Invoke();

    /// <summary>An arrow key: -1 previous, +1 next. Returns false when there is nothing to move to.</summary>
    bool Move(int delta);
}

/// <summary>
/// The general <see cref="IAccessibleItem"/>: a content view the owning control fills with the
/// part's visuals and wires through <see cref="Invoked"/> and <see cref="MoveRequested"/>.
/// </summary>
public class AccessibleItemView : ContentView, IAccessibleItem
{
    public static readonly BindableProperty RoleProperty = BindableProperty.Create(
        nameof(Role), typeof(AccessibleItemRole), typeof(AccessibleItemView), AccessibleItemRole.Button);

    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name), typeof(string), typeof(AccessibleItemView));

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(AccessibleItemView), false);

    public static readonly BindableProperty FocusGroupProperty = BindableProperty.Create(
        nameof(FocusGroup), typeof(string), typeof(AccessibleItemView));

    public AccessibleItemRole Role
    {
        get => (AccessibleItemRole)GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>Selected (a tab, a radio choice) or expanded (a disclosure).</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string? FocusGroup
    {
        get => (string?)GetValue(FocusGroupProperty);
        set => SetValue(FocusGroupProperty, value);
    }

    public IReadOnlyList<AccessibleItemAction>? Actions { get; set; }

    /// <summary>Space or Return while the item has keyboard focus, or VoiceOver's activate.</summary>
    public event EventHandler? Invoked;

    /// <summary>An arrow key while the item has keyboard focus; set Handled when the owner moved.</summary>
    public event EventHandler<AccessibleItemMoveEventArgs>? MoveRequested;

    AccessibleItemRole IAccessibleItem.AccessibleRole => Role;

    string? IAccessibleItem.AccessibleName => Name;

    bool IAccessibleItem.IsItemSelected => IsSelected;

    IReadOnlyList<AccessibleItemAction>? IAccessibleItem.AccessibleActions => Actions;

    void IAccessibleItem.Invoke()
    {
        if (IsEnabled)
        {
            Invoked?.Invoke(this, EventArgs.Empty);
        }
    }

    bool IAccessibleItem.Move(int delta)
    {
        if (!IsEnabled || MoveRequested is null)
        {
            return false;
        }

        var args = new AccessibleItemMoveEventArgs(delta);
        MoveRequested(this, args);
        return args.Handled;
    }
}

public sealed class AccessibleItemMoveEventArgs(int delta) : EventArgs
{
    /// <summary>-1 for the previous item (← or ↑), +1 for the next (→ or ↓).</summary>
    public int Delta { get; } = delta;

    public bool Handled { get; set; }
}

/// <summary>Keyboard focus for <see cref="IAccessibleItem"/>s.</summary>
public static class AccessibleItemFocus
{
    /// <summary>
    /// Moves keyboard focus to <paramref name="item"/> (after an arrow key selected it), when focus
    /// is on one of its siblings; otherwise leaves focus where it is.
    /// </summary>
    public static void MoveTo(IView item) => item.Handler?.Invoke(FocusCommand);

    internal const string FocusCommand = "AccessibleItemFocus";

    /// <summary>A stable focus-group identifier for the parts of one control instance.</summary>
    public static string GroupFor(object owner) => $"{owner.GetType().Name}-{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner)}";
}
