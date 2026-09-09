using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A combo box whose rows are rendered from a <see cref="DataTemplate"/>, so every
/// item can be an arbitrary view instead of just text (or text + icon).
/// </summary>
/// <remarks>
/// The platform handler builds one templated view per item and hosts it inside a
/// UIKit popover, which is why this cannot be done with <c>UIButton.Menu</c>:
/// <c>UIMenu</c> items only ever carry a title, an image and a state.
/// </remarks>
public sealed class TemplatedComboBox : View
{
    /// <summary>Set while SelectedItem is pushing its index across, to keep the two from ping-ponging.</summary>
    private bool _syncingSelection;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(TemplatedComboBox), null,
            propertyChanged: OnItemsSourceChanged);

    /// <summary>Template for a row in the drop-down. Receives the item as its binding context.</summary>
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(TemplatedComboBox), null);

    /// <summary>
    /// Template for the collapsed field. Falls back to <see cref="ItemTemplate"/> when unset,
    /// which covers the common case where the field should look like the row.
    /// </summary>
    public static readonly BindableProperty SelectedItemTemplateProperty =
        BindableProperty.Create(nameof(SelectedItemTemplate), typeof(DataTemplate), typeof(TemplatedComboBox), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(TemplatedComboBox), null,
            BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(TemplatedComboBox), "Select…");

    /// <summary>Cap on the drop-down's height; past this it scrolls.</summary>
    public static readonly BindableProperty MaxDropDownHeightProperty =
        BindableProperty.Create(nameof(MaxDropDownHeight), typeof(double), typeof(TemplatedComboBox), 260d);

    /// <summary>Two-way, so the drop-down can be opened or closed from a view model.</summary>
    public static readonly BindableProperty IsDropDownOpenProperty =
        BindableProperty.Create(nameof(IsDropDownOpen), typeof(bool), typeof(TemplatedComboBox), false,
            BindingMode.TwoWay);

    /// <summary>Index of <see cref="SelectedItem"/> in <see cref="ItemsSource"/>, or -1.</summary>
    /// <remarks>
    /// Kept because UWP code-behind selects by index far more often than by item; the two
    /// properties stay in sync in both directions.
    /// </remarks>
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(TemplatedComboBox), -1,
            BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    /// <summary>Font size for the placeholder text. Templated rows style themselves.</summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(TemplatedComboBox), 14.0);

    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(TemplatedComboBox), 1.0);

    /// <summary>Carried over from UWP's ComboBox.Header; the localization shim writes to it.</summary>
    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(nameof(Header), typeof(object), typeof(TemplatedComboBox), null);

    public static readonly BindableProperty TagProperty =
        BindableProperty.Create(nameof(Tag), typeof(object), typeof(TemplatedComboBox), null);

    /// <summary>
    /// Space between the field's border and its content. Declared here because the control is a
    /// bare <see cref="View"/> - it has no layout of its own to inherit Padding from - and UWP
    /// ComboBoxes routinely set it to tighten the field.
    /// </summary>
    /// <summary>
    /// Radius of the field's rounded corners, in points. Zero gives the square field the
    /// converted UWP views ask for with CornerRadius="0".
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(TemplatedComboBox), 0d);

    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(TemplatedComboBox),
            new Thickness(4));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public DataTemplate? SelectedItemTemplate
    {
        get => (DataTemplate?)GetValue(SelectedItemTemplateProperty);
        set => SetValue(SelectedItemTemplateProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>UWP named its element accessor Name; MAUI stores it as the automation id.</summary>
    public string? Name
    {
        get => AutomationId;
        set => AutomationId = value;
    }

    /// <summary>The materialised items, for code-behind that reads ComboBox.Items.</summary>
    public IReadOnlyList<object> Items => ItemList;

    /// <summary>Alias kept for UWP code-behind that assigns ComboBox.SelectedValue.</summary>
    public object? SelectedValue
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    // The converter rewrites every `x.SelectedIndex` access into these helpers, because most
    // MAUI targets of that rewrite have no such property.
    public int GetSelectedIndex() => SelectedIndex;

    public void SetSelectedIndex(int value) => SelectedIndex = value;

    /// <summary>
    /// Raised whenever the selection changes, however it changed.
    /// </summary>
    /// <remarks>
    /// Typed with MAUI's <see cref="SelectionChangedEventArgs"/> rather than a bespoke args
    /// class so that handlers converted from UWP - which all carry the
    /// <c>(object sender, SelectionChangedEventArgs e)</c> signature - bind without a rewrite.
    /// The args instance is null, matching the plain ComboBox: converted handlers read the
    /// selection off the control, never off the event.
    /// </remarks>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Raised before the popover is shown. Set <see cref="CancelEventArgs.Cancel"/> to
    /// stop it opening — useful for populating items on demand, or vetoing the open.
    /// </summary>
    public event EventHandler<CancelEventArgs>? BeforePopoverOpen;

    /// <summary>Raised once the popover is on screen and its open animation has finished.</summary>
    public event EventHandler? PopoverOpened;

    /// <summary>Raised once the popover has been dismissed, however it was dismissed.</summary>
    public event EventHandler? PopoverClosed;

    /// <summary>Returns false when a handler cancelled the open.</summary>
    internal bool RaiseBeforePopoverOpen()
    {
        if (BeforePopoverOpen is null)
            return true;

        var args = new CancelEventArgs();
        BeforePopoverOpen.Invoke(this, args);
        return !args.Cancel;
    }

    internal void RaisePopoverOpened() => PopoverOpened?.Invoke(this, EventArgs.Empty);

    internal void RaisePopoverClosed() => PopoverClosed?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the items collection is replaced or mutated, so the handler can rebuild.</summary>
    internal event EventHandler? ItemsChanged;

    /// <summary>The effective template for the collapsed field.</summary>
    internal DataTemplate? EffectiveSelectedItemTemplate => SelectedItemTemplate ?? ItemTemplate;

    /// <summary>Items as a list, materialised once so the handler can index into it.</summary>
    internal List<object> ItemList { get; private set; } = [];

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not TemplatedComboBox combo)
            return;

        if (oldValue is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= combo.OnCollectionChanged;

        if (newValue is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += combo.OnCollectionChanged;

        combo.RebuildItemList();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItemList();

    private void RebuildItemList()
    {
        ItemList = ItemsSource?.Cast<object>().ToList() ?? [];

        // Drop a selection that is no longer in the list; otherwise re-derive its index,
        // which the rebuild may have shifted.
        if (SelectedItem is not null && !ItemList.Contains(SelectedItem))
            SelectedItem = null;
        else
            SyncSelectedIndexFromItem(SelectedItem);

        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is not TemplatedComboBox combo)
            return;

        combo.SyncSelectedIndexFromItem(newValue);
        combo.SelectionChanged?.Invoke(combo, default!);
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        // When the index is only echoing a selection made by item, SelectedItem has already
        // raised SelectionChanged; going back the other way would raise it a second time.
        if (bindable is not TemplatedComboBox combo || combo._syncingSelection)
            return;

        var index = newValue is int value ? value : -1;
        combo.SelectedItem = index >= 0 && index < combo.ItemList.Count ? combo.ItemList[index] : null;
    }

    private void SyncSelectedIndexFromItem(object? item)
    {
        _syncingSelection = true;
        try
        {
            SelectedIndex = item is null ? -1 : ItemList.IndexOf(item);
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
