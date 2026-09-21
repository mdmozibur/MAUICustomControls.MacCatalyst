using System;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A control that draws its own PointerOver state from a PointerGestureRecognizer and wants it inside
/// the window's title bar band too, where UIKit sends no hover (see TitleBandHover in
/// Platforms/MacCatalyst/InteractionStates.cs).
/// </summary>
internal interface ITitleBandHoverTarget
{
    void SetTitleBandPointerOver(bool isPointerOver);
}

public sealed partial class TabItem : TemplatedView, IAccessibleItem, ITitleBandHoverTarget
{
    private const string NormalStateName = "Normal";
    private const string PointerOverStateName = "PointerOver";
    private const string SelectedStateName = "Selected";

    private Grid? _rootGrid;
    private Button? _closeButton;
    private BoxView? _selectionHighlighter;
    private bool _isPointerOver;

    internal event EventHandler? SelectionRequested;

    internal event EventHandler? CloseRequested;

    // UWP TabItem.DuplicateRequested, from the tab's context menu.
    internal event EventHandler? DuplicateRequested;

    // ← or → while the tab has keyboard focus (see IAccessibleItem).
    internal event EventHandler<AccessibleItemMoveEventArgs>? MoveRequested;

    /// <summary>The tab strip's keyboard focus group, set by the tab view.</summary>
    internal string? FocusGroup { get; set; }

    // The tab's context menu (UWP TabContextFlyout): copy the document's full path, duplicate it.
    private readonly ContextMenuFlyout _contextMenu = new();
    private readonly AppMenuItem _copyPathItem = new();
    private readonly AppMenuItem _duplicateItem = new();

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(TabItem), false, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty CanCopyFilePathProperty =
        BindableProperty.Create(nameof(CanCopyFilePath), typeof(bool), typeof(TabItem), false, propertyChanged: OnFilePathChanged);

    public static readonly BindableProperty FilePathProperty =
        BindableProperty.Create(nameof(FilePath), typeof(string), typeof(TabItem), null, propertyChanged: OnFilePathChanged);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TabItem), null, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty IsClosableProperty =
        BindableProperty.Create(nameof(IsClosable), typeof(bool), typeof(TabItem), true, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty SelectionBarHeightProperty =
        BindableProperty.Create(nameof(SelectionBarHeight), typeof(double), typeof(TabItem), 2d, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty UnsavedDotVisiblityProperty =
        BindableProperty.Create(nameof(UnsavedDotVisiblity), typeof(bool), typeof(TabItem), true, propertyChanged: OnVisualPropertyChanged);

    // UWP TabItem.IconGlyph: the document-type glyph before the title, in IconFontFamily.
    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(TabItem), null);

    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(nameof(IconFontFamily), typeof(string), typeof(TabItem), null);

    // The document has changes that are not saved: UWP paints the selection bar Chocolate on the
    // selected tab and a dim brown on the others instead of DodgerBlue / nothing.
    public static readonly BindableProperty IsUnsavedProperty =
        BindableProperty.Create(nameof(IsUnsaved), typeof(bool), typeof(TabItem), false, propertyChanged: OnVisualPropertyChanged);

    // UWP ignores the pointer over a read-only document's tab.
    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(TabItem), false, propertyChanged: OnReadOnlyChanged);

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // Whether the tab's document was opened from (or saved to) a file whose path may be copied.
    public bool CanCopyFilePath
    {
        get => (bool)GetValue(CanCopyFilePathProperty);
        set => SetValue(CanCopyFilePathProperty, value);
    }

    public string? FilePath
    {
        get => (string?)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public double SelectionBarHeight
    {
        get => (double)GetValue(SelectionBarHeightProperty);
        set => SetValue(SelectionBarHeightProperty, value);
    }

    public bool UnsavedDotVisiblity
    {
        get => (bool)GetValue(UnsavedDotVisiblityProperty);
        set => SetValue(UnsavedDotVisiblityProperty, value);
    }

    public string? IconGlyph
    {
        get => (string?)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string? IconFontFamily
    {
        get => (string?)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }

    public bool IsUnsaved
    {
        get => (bool)GetValue(IsUnsavedProperty);
        set => SetValue(IsUnsavedProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public TabItem()
    {
        _copyPathItem.Clicked += CopyPathItem_Clicked;
        _duplicateItem.Clicked += DuplicateItem_Clicked;
        _contextMenu.Add(_copyPathItem);
        _contextMenu.Add(_duplicateItem);
        _contextMenu.Opening += (_, _) => UpdateContextMenu();
        FlyoutBase.SetContextFlyout(this, _contextMenu);
        UpdateContextMenu();
    }

    // An app supplies these in a partial part (the converter's TabItem.Compatibility.cs): which
    // path may be copied, whether the tab offers the menu at all, and the localized item titles.
    partial void ResolveCopyableFilePath(ref string? path);

    partial void ResolveContextMenuAvailability(ref bool isAvailable);

    partial void LocalizeContextMenu(ref string copyPathText, ref string duplicateText);

    private string? GetCopyableFilePath()
    {
        var path = CanCopyFilePath && !string.IsNullOrWhiteSpace(FilePath) ? FilePath : null;
        ResolveCopyableFilePath(ref path);
        return path;
    }

    // Re-evaluated when the menu opens: the path can change after Save As without a new tab.
    private void UpdateContextMenu()
    {
        var isAvailable = true;
        ResolveContextMenuAvailability(ref isAvailable);

        var copyPathText = "Copy Full Path";
        var duplicateText = "Duplicate";
        LocalizeContextMenu(ref copyPathText, ref duplicateText);
        _copyPathItem.Text = copyPathText;
        _duplicateItem.Text = duplicateText;

        var path = isAvailable ? GetCopyableFilePath() : null;
        _copyPathItem.IsVisible = path is not null;
        _duplicateItem.IsVisible = isAvailable;
        ToolTipProperties.SetText(this, path);
    }

    private static void OnFilePathChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((TabItem)bindable).UpdateContextMenu();
    }

    private async void CopyPathItem_Clicked(object? sender, EventArgs e)
    {
        if (GetCopyableFilePath() is { } path)
        {
            await global::Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(path);
        }
    }

    private void DuplicateItem_Clicked(object? sender, EventArgs e)
    {
        var isAvailable = true;
        ResolveContextMenuAvailability(ref isAvailable);
        if (isAvailable)
        {
            DuplicateRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_closeButton is not null) _closeButton.Clicked -= CloseButton_Clicked;

        _rootGrid = GetTemplateChild("RootGrid") as Grid;
        _closeButton = GetTemplateChild("CloseButton") as Button;
        _selectionHighlighter = GetTemplateChild("SelectionHighlighter") as BoxView;

        if (_closeButton is not null) _closeButton.Clicked += CloseButton_Clicked;

        if (_rootGrid is not null)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += Root_Tapped;
            _rootGrid.GestureRecognizers.Add(tapGesture);

            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += Pointer_Entered;
            pointerGesture.PointerExited += Pointer_Exited;
            _rootGrid.GestureRecognizers.Add(pointerGesture);
        }

        UpdateVisualState();
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((TabItem)bindable).UpdateVisualState();
    }

    private void Root_Tapped(object? sender, TappedEventArgs e)
    {
        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Clicked(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, e);
    }

    private void Pointer_Entered(object? sender, PointerEventArgs e)
    {
        if (IsReadOnly)
            return;

        _isPointerOver = true;
        UpdateVisualState();
    }

    // The document tabs sit in the title bar band.
    void ITitleBandHoverTarget.SetTitleBandPointerOver(bool isPointerOver)
    {
        if (isPointerOver)
        {
            Pointer_Entered(this, null!);
        }
        else
        {
            Pointer_Exited(this, null!);
        }
    }

    private static void OnReadOnlyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var tab = (TabItem)bindable;
        if (newValue is true)
            tab._isPointerOver = false;

        tab.UpdateVisualState();
    }

    private void Pointer_Exited(object? sender, PointerEventArgs e)
    {
        _isPointerOver = false;
        UpdateVisualState();
    }

    // A document tab to the keyboard and VoiceOver: Space/Return select it, ← and → move to the
    // neighbouring tab, and VoiceOver offers Close as an action (the close button is inside the tab).
    AccessibleItemRole IAccessibleItem.AccessibleRole => AccessibleItemRole.Tab;

    string? IAccessibleItem.AccessibleName => Title;

    bool IAccessibleItem.IsItemSelected => IsSelected;

    string? IAccessibleItem.FocusGroup => FocusGroup;

    IReadOnlyList<AccessibleItemAction>? IAccessibleItem.AccessibleActions => IsClosable
        ? [new AccessibleItemAction(_closeButton is { } button && SemanticProperties.GetDescription(button) is { Length: > 0 } name ? name : "Close", () => CloseRequested?.Invoke(this, EventArgs.Empty))]
        : null;

    void IAccessibleItem.Invoke() => SelectionRequested?.Invoke(this, EventArgs.Empty);

    bool IAccessibleItem.Move(int delta)
    {
        var args = new AccessibleItemMoveEventArgs(delta);
        MoveRequested?.Invoke(this, args);
        return args.Handled;
    }

    private void UpdateVisualState()
    {
        if (_rootGrid is null)
            return;

        VisualStateManager.GoToState(_rootGrid, GetVisualStateName());

        // UWP ChangeState / History_SavedUnsaved_Changed: the selection bar also tells whether the
        // document is saved.
        if (_selectionHighlighter is not null)
        {
            var unsaved = IsUnsaved && UnsavedDotVisiblity;
            _selectionHighlighter.Color = IsSelected
                ? (unsaved ? Colors.Chocolate : Colors.DodgerBlue)
                : (unsaved ? Color.FromRgba(123, 63, 0, 130) : Colors.Transparent);
        }
    }

    private string GetVisualStateName()
    {
        if (IsSelected)
            return SelectedStateName;

        return _isPointerOver ? PointerOverStateName : NormalStateName;
    }
}
