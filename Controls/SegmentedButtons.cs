using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MAUICustomControls.MacCatalyst.Controls;

[ContentProperty(nameof(Content))]
public sealed class SegmentedButtonItem : BindableObject
{
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(object),
        typeof(SegmentedButtonItem),
        propertyChanged: OnPresentationChanged);

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(object),
        typeof(SegmentedButtonItem),
        propertyChanged: OnPresentationChanged);

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    internal string GetContentText()
    {
        return GetText(Content) ?? string.Empty;
    }

    /// <summary>The icon's glyph and the icon font it is drawn with, if the item has an icon.</summary>
    internal string? GetIconGlyph(out string? fontFamily)
    {
        switch (Icon)
        {
            case Label label:
                fontFamily = label.FontFamily;
                return label.Text;
            case FontImageSource fontImage:
                fontFamily = fontImage.FontFamily;
                return fontImage.Glyph;
            case string glyph:
                fontFamily = null;
                return glyph;
            default:
                fontFamily = null;
                return null;
        }
    }

    private static string? GetText(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            Label label => label.Text,
            _ => value.ToString(),
        };
    }

    private static void OnPresentationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        _ = oldValue;
        _ = newValue;

        if (bindable is SegmentedButtonItem item)
        {
            item.PresentationChanged?.Invoke(item, EventArgs.Empty);
        }
    }

    internal event EventHandler? PresentationChanged;
}

public sealed class SegmentedButtonsSelectionChangedEventArgs : EventArgs
{
    public SegmentedButtonsSelectionChangedEventArgs(
        int oldIndex,
        int newIndex,
        SegmentedButtonItem? oldItem,
        SegmentedButtonItem? newItem)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        OldItem = oldItem;
        NewItem = newItem;
    }

    public int OldIndex { get; }

    public int NewIndex { get; }

    public SegmentedButtonItem? OldItem { get; }

    public SegmentedButtonItem? NewItem { get; }
}

public delegate void SegmentedButtonsSelectionChangedEventHandler(
    SegmentedButtons sender,
    SegmentedButtonsSelectionChangedEventArgs e);

[ContentProperty(nameof(Items))]
public sealed class SegmentedButtons : View
{
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header),
        typeof(object),
        typeof(SegmentedButtons));

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SegmentedButtons),
        0,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedIndexChanged,
        coerceValue: CoerceSelectedIndex);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(SegmentedButtons),
        13d);

    // Null means "follow the theme": the handler falls back to the dynamic label colour.
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SegmentedButtons));

    public static readonly BindableProperty SelectedTextColorProperty = BindableProperty.Create(
        nameof(SelectedTextColor),
        typeof(Color),
        typeof(SegmentedButtons),
        Colors.White);

    public static readonly BindableProperty TintColorProperty = BindableProperty.Create(
        nameof(TintColor),
        typeof(Color),
        typeof(SegmentedButtons),
        Colors.DodgerBlue);

    private readonly ObservableCollection<SegmentedButtonItem> _items = [];

    public SegmentedButtons()
    {
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    public ObservableCollection<SegmentedButtonItem> Items => _items;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public SegmentedButtonItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color SelectedTextColor
    {
        get => (Color)GetValue(SelectedTextColorProperty);
        set => SetValue(SelectedTextColorProperty, value);
    }

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public event SegmentedButtonsSelectionChangedEventHandler? SelectionChanged;

    public int GetSelectedIndex() => SelectedIndex;

    public void SetSelectedIndex(int value) => SelectedIndex = value;

    internal string GetHeaderText()
    {
        return Header switch
        {
            null => string.Empty,
            string text => text,
            Label label => label.Text ?? string.Empty,
            _ => Header.ToString() ?? string.Empty,
        };
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (SegmentedButtons)bindable;
        var oldIndex = (int)oldValue;
        var newIndex = (int)newValue;

        control.SelectionChanged?.Invoke(
            control,
            new SegmentedButtonsSelectionChangedEventArgs(
                oldIndex,
                newIndex,
                control.GetItem(oldIndex),
                control.GetItem(newIndex)));
    }

    private static object CoerceSelectedIndex(BindableObject bindable, object value)
    {
        var control = (SegmentedButtons)bindable;
        var selectedIndex = (int)value;

        if (control.Items.Count == 0)
        {
            return Math.Max(-1, selectedIndex);
        }

        return Math.Clamp(selectedIndex, -1, control.Items.Count - 1);
    }

    private SegmentedButtonItem? GetItem(int index)
    {
        return index >= 0 && index < Items.Count ? Items[index] : null;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SegmentedButtonItem item in e.OldItems)
            {
                item.PresentationChanged -= OnItemPresentationChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SegmentedButtonItem item in e.NewItems)
            {
                item.PresentationChanged += OnItemPresentationChanged;
            }
        }

        CoerceValue(SelectedIndexProperty);
        Handler?.UpdateValue(nameof(Items));
        Handler?.UpdateValue(nameof(SelectedIndex));
    }

    private void OnItemPresentationChanged(object? sender, EventArgs e)
    {
        Handler?.UpdateValue(nameof(Items));
    }
}
