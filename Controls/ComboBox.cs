using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A drop-down selector: a field showing the current selection and a list of the items below it.
/// </summary>
/// <remarks>
/// Rows are plain text by default (the item's <see cref="DisplayMemberPath"/> member, or its
/// ToString()), and arbitrary views once an <see cref="ItemTemplate"/> is set. Items come from
/// <see cref="ItemsSource"/> or are added to <see cref="Items"/> directly, as with UWP's ComboBox.
/// The platform handler hosts the list in its own presentation rather than a <c>UIMenu</c>, whose
/// entries only ever carry a title, an image and a state.
/// </remarks>
public sealed class ComboBox : View
{
    private readonly ItemCollection _items = new();

    /// <summary>Set while a selection is written to both properties, to keep them from ping-ponging.</summary>
    private bool _syncingSelection;

    // The selection SelectionChanged was last raised for. SelectedItem and SelectedIndex can run
    // ahead of it while they wait for the items to arrive.
    private object? _committedItem;
    private int _committedIndex = -1;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ComboBox), null,
            propertyChanged: OnItemsSourceChanged);

    /// <summary>Template for a row in the drop-down. Receives the item as its binding context.</summary>
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(ComboBox), null);

    /// <summary>
    /// Template for the collapsed field. Falls back to <see cref="ItemTemplate"/> when unset,
    /// which covers the common case where the field should look like the row.
    /// </summary>
    public static readonly BindableProperty SelectedItemTemplateProperty =
        BindableProperty.Create(nameof(SelectedItemTemplate), typeof(DataTemplate), typeof(ComboBox), null);

    /// <summary>Path of the member shown for an item when there is no <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ComboBox), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ComboBox), null,
            BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    /// <summary>Index of <see cref="SelectedItem"/> in <see cref="Items"/>, or -1.</summary>
    /// <remarks>
    /// Kept because UWP code-behind selects by index far more often than by item; the two
    /// properties stay in sync in both directions.
    /// </remarks>
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(ComboBox), -1,
            BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    /// <summary>Shown in the field while nothing is selected.</summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ComboBox), string.Empty);

    /// <summary>Cap on the drop-down's height; past this it scrolls.</summary>
    public static readonly BindableProperty MaxDropDownHeightProperty =
        BindableProperty.Create(nameof(MaxDropDownHeight), typeof(double), typeof(ComboBox), 260d);

    /// <summary>Two-way, so the drop-down can be opened or closed from a view model.</summary>
    public static readonly BindableProperty IsDropDownOpenProperty =
        BindableProperty.Create(nameof(IsDropDownOpen), typeof(bool), typeof(ComboBox), false,
            BindingMode.TwoWay);

    /// <summary>Font size of the placeholder and of text rows. Templated rows style themselves.</summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ComboBox), 14.0);

    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ComboBox), 1.0);

    /// <summary>Caption drawn above the field: text, or a view.</summary>
    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(nameof(Header), typeof(object), typeof(ComboBox), null);

    public static readonly BindableProperty TagProperty =
        BindableProperty.Create(nameof(Tag), typeof(object), typeof(ComboBox), null);

    /// <summary>
    /// Radius of the field's rounded corners, in points. Zero gives the square field the
    /// converted UWP views ask for with CornerRadius="0".
    /// </summary>
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(ComboBox), 0d);

    /// <summary>
    /// Space between the field's border and its content. Declared here because the control is a
    /// bare <see cref="View"/> - it has no layout of its own to inherit Padding from - and UWP
    /// ComboBoxes routinely set it to tighten the field.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(ComboBox),
            new Thickness(4));

    public ComboBox()
    {
        _items.CollectionChanged += (_, _) => OnItemsChanged();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// The items in the list. Mirrors <see cref="ItemsSource"/> when that is set; otherwise it is
    /// filled directly, which is how UWP code-behind (Items.Add) and inline XAML items populate a box.
    /// </summary>
    public IList<object> Items => _items;

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

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

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

    /// <summary>
    /// Raised whenever the selection changes, however it changed.
    /// </summary>
    /// <remarks>
    /// Typed with MAUI's <see cref="SelectionChangedEventArgs"/> rather than a bespoke args
    /// class so that handlers converted from UWP - which all carry the
    /// <c>(object sender, SelectionChangedEventArgs e)</c> signature - bind without a rewrite.
    /// The args instance is null: converted handlers read the selection off the control, never
    /// off the event.
    /// </remarks>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Raised as the drop-down opens, before its rows are built, so a handler can still refresh
    /// the items.
    /// </summary>
    public event EventHandler<object>? DropDownOpened;

    /// <summary>Raised once the drop-down has been dismissed, however it was dismissed.</summary>
    public event EventHandler<object>? DropDownClosed;

    internal void RaiseDropDownOpened() => DropDownOpened?.Invoke(this, EventArgs.Empty);

    internal void RaiseDropDownClosed() => DropDownClosed?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised when the items are replaced or mutated, so the handler can rebuild an open list.</summary>
    internal event EventHandler? ItemsChanged;

    /// <summary>The effective template for the collapsed field.</summary>
    internal DataTemplate? EffectiveSelectedItemTemplate => SelectedItemTemplate ?? ItemTemplate;

    internal IReadOnlyList<object> ItemList => _items;

    /// <summary>The text a row shows for an item when no template and no DisplayMemberPath apply.</summary>
    internal static string ItemText(object? item) => item switch
    {
        null => string.Empty,
        SelectorOption option => option.Text,
        _ => item.ToString() ?? string.Empty,
    };

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not ComboBox combo)
            return;

        if (oldValue is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= combo.OnSourceCollectionChanged;

        if (newValue is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += combo.OnSourceCollectionChanged;

        combo._items.ReplaceAll(newValue as IEnumerable);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _items.ReplaceAll(ItemsSource);

    private void OnItemsChanged()
    {
        if (SelectedItem is { } item)
        {
            // Confirms a selection made before its item arrived, follows an index the change
            // shifted, and drops a selection that is no longer in the list.
            var index = _items.IndexOf(item);
            if (index >= 0)
                Commit(item, index, raiseOnIndexChange: false);
            else
                Commit(null, -1);
        }
        else if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
        {
            // XAML sets SelectedIndex="1" before the items arrive (attributes are applied before
            // child elements); UWP selects that item once it exists.
            Commit(_items[SelectedIndex], SelectedIndex);
        }

        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is not ComboBox combo || combo._syncingSelection)
            return;

        if (newValue is null)
        {
            combo.Commit(null, -1);
            return;
        }

        var index = combo._items.IndexOf(newValue);
        if (index >= 0)
            combo.Commit(newValue, index);
        else if (combo._items.Count > 0)
            combo.Commit(null, -1); // As in UWP, an item that is not in the list cannot be selected.

        // With no items yet the value waits for them: a binding may set SelectedItem before
        // ItemsSource, and OnItemsChanged settles it once the list exists.
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is not ComboBox combo || combo._syncingSelection)
            return;

        var index = newValue is int value ? value : -1;
        if (index >= 0 && index < combo._items.Count)
            combo.Commit(combo._items[index], index);
        else if (index < 0 || combo._items.Count > 0)
            combo.Commit(null, -1);

        // An index set before any item exists waits for the items, like SelectedItem above.
    }

    /// <summary>The one place a selection is written: both properties, then one SelectionChanged.</summary>
    private void Commit(object? item, int index, bool raiseOnIndexChange = true)
    {
        var changed = !Equals(_committedItem, item) || (raiseOnIndexChange && _committedIndex != index);
        _committedItem = item;
        _committedIndex = index;

        _syncingSelection = true;
        try
        {
            SelectedItem = item;
            SelectedIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }

        if (changed)
            SelectionChanged?.Invoke(this, default!);
    }

    /// <summary>
    /// The item list. Replacing its contents raises one Reset instead of a change per item, so a
    /// new ItemsSource settles the selection once.
    /// </summary>
    private sealed class ItemCollection : ObservableCollection<object>
    {
        public void ReplaceAll(IEnumerable? source)
        {
            Items.Clear();

            if (source is not null)
            {
                foreach (var item in source)
                    Items.Add(item!);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
