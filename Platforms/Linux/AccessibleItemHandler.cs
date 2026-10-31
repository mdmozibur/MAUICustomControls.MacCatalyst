namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MAUICustomControls.MacCatalyst.Controls;
using ContentViewHandler = Avalonia.Controls.Maui.Handlers.ContentViewHandler;
using PlatformContentView = Avalonia.Controls.Maui.Platform.ContentView;

/// <summary>Keyboard activation and group navigation for tab, radio, and disclosure headers.</summary>
public sealed class AccessibleItemHandler : ContentViewHandler
{
    public static readonly PropertyMapper<IContentView, AccessibleItemHandler> ItemMapper = new(ContentViewHandler.Mapper)
    {
        [nameof(AccessibleItemView.Role)] = UpdateItem,
        [nameof(AccessibleItemView.Name)] = UpdateItem,
        [nameof(AccessibleItemView.IsSelected)] = UpdateItem,
        [nameof(AccessibleItemView.FocusGroup)] = UpdateItem,
        ["Title"] = UpdateItem,
    };

    public static readonly CommandMapper<IContentView, AccessibleItemHandler> ItemCommands = new(ContentViewHandler.CommandMapper)
    {
        [AccessibleItemFocus.FocusCommand] = (handler, _, _) => handler.MoveFocusHere(),
    };

    public AccessibleItemHandler() : base(ItemMapper, ItemCommands) { }

    protected override PlatformContentView CreatePlatformView() => new AccessibleItemPlatformView
    {
        CrossPlatformLayout = VirtualView,
        Item = VirtualView as IAccessibleItem,
    };

    protected override void DisconnectHandler(PlatformContentView platformView)
    {
        ((AccessibleItemPlatformView)platformView).Item = null;
        base.DisconnectHandler(platformView);
    }

    private static void UpdateItem(AccessibleItemHandler handler, IContentView view)
    {
        if (handler.PlatformView is AccessibleItemPlatformView platformView)
        {
            platformView.UpdateItem();
        }
    }

    private void MoveFocusHere()
    {
        if (PlatformView is not AccessibleItemPlatformView { Item: { } item } target ||
            TopLevel.GetTopLevel(target)?.FocusManager?.GetFocusedElement() is not AccessibleItemPlatformView focused ||
            string.IsNullOrEmpty(item.FocusGroup) || focused.Item?.FocusGroup != item.FocusGroup)
        {
            return;
        }
        target.Focus(NavigationMethod.Directional);
    }
}

public sealed class AccessibleItemPlatformView : PlatformContentView
{
    public IAccessibleItem? Item { get; set; }
    private bool _lastSelected;

    public AccessibleItemPlatformView()
    {
        Focusable = true;
        FocusAdorner = new Avalonia.Controls.Templates.FuncTemplate<Control>(() => new Border
        {
            BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(3), IsHitTestVisible = false,
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateItem();
    }

    internal void UpdateItem()
    {
        AutomationProperties.SetName(this, Item?.AccessibleName ?? Item?.Semantics?.Description);
        var isSelected = Item?.IsItemSelected == true;
        if (_lastSelected != isSelected && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            if (Item?.AccessibleRole == AccessibleItemRole.Disclosure)
                peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    _lastSelected ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                    isSelected ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            else if (Item?.AccessibleRole is AccessibleItemRole.Tab or AccessibleItemRole.RadioButton)
                peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, _lastSelected, isSelected);
        }
        _lastSelected = isSelected;
        if (TopLevel.GetTopLevel(this) is { } root)
        {
            var siblings = root.GetVisualDescendants().OfType<AccessibleItemPlatformView>()
                .Where(view => !string.IsNullOrEmpty(Item?.FocusGroup) && view.Item?.FocusGroup == Item.FocusGroup).ToList();
            var selected = siblings.FirstOrDefault(view => view.Item?.IsItemSelected == true && view.IsEffectivelyVisible && view.IsEffectivelyEnabled)
                ?? siblings.FirstOrDefault(view => view.IsEffectivelyVisible && view.IsEffectivelyEnabled);
            foreach (var sibling in siblings)
            {
                KeyboardNavigation.SetIsTabStop(sibling, ReferenceEquals(sibling, selected));
            }
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // A nested entry or button keeps its own keys; only the focused header activates here.
        if (!IsFocused || e.Handled || e.KeyModifiers != KeyModifiers.None || Item is not { IsEnabled: true } item)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.Key is Key.Space or Key.Enter)
        {
            item.Invoke();
            e.Handled = true;
        }
        else if (item.AccessibleRole == AccessibleItemRole.Disclosure && e.Key is Key.Left or Key.Right)
        {
            if (item.IsItemSelected != (e.Key == Key.Right)) item.Invoke();
            e.Handled = true;
        }
        else if (item.AccessibleRole is AccessibleItemRole.Tab or AccessibleItemRole.RadioButton)
        {
            var delta = e.Key switch
            {
                Key.Left => -1, Key.Right => 1,
                Key.Up when item.AccessibleRole == AccessibleItemRole.RadioButton => -1,
                Key.Down when item.AccessibleRole == AccessibleItemRole.RadioButton => 1,
                _ => 0,
            };
            if (delta != 0) e.Handled = item.Move(delta);
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccessibleItemPeer(this);

    private sealed class AccessibleItemPeer(AccessibleItemPlatformView owner) : ControlAutomationPeer(owner),
        IInvokeProvider, ISelectionItemProvider, IExpandCollapseProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Item?.AccessibleRole switch
        {
            AccessibleItemRole.Tab => AutomationControlType.TabItem,
            AccessibleItemRole.RadioButton => AutomationControlType.RadioButton,
            AccessibleItemRole.Disclosure => AutomationControlType.Expander,
            _ => AutomationControlType.Button,
        };

        public void Invoke()
        {
            EnsureEnabled();
            if (owner.Item is { IsEnabled: true } item) item.Invoke();
        }

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(ISelectionItemProvider))
                return owner.Item?.AccessibleRole is AccessibleItemRole.Tab or AccessibleItemRole.RadioButton ? this : null;
            if (providerType == typeof(IExpandCollapseProvider))
                return owner.Item?.AccessibleRole == AccessibleItemRole.Disclosure ? this : null;
            return base.GetProviderCore(providerType);
        }

        public bool IsSelected => owner.Item?.IsItemSelected == true;
        public ISelectionProvider? SelectionContainer => null;
        public void Select()
        {
            EnsureEnabled();
            if (!IsSelected) Invoke();
        }
        public void AddToSelection()
        {
            if (!IsSelected) throw new InvalidOperationException("The group permits a single selected item. Use Select to change it.");
        }
        public void RemoveFromSelection()
        {
            if (IsSelected) throw new InvalidOperationException("The selected item cannot be removed from this group.");
        }

        public ExpandCollapseState ExpandCollapseState => IsSelected ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public bool ShowsMenu => false;
        public void Expand() { EnsureEnabled(); if (!IsSelected) Invoke(); }
        public void Collapse() { EnsureEnabled(); if (IsSelected) Invoke(); }
    }
}
