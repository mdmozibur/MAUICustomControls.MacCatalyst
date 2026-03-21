using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ComboBox : ContentView
{
    private bool _syncingSelection;
    private readonly ObservableCollection<object> _items = [];

    public event EventHandler<object>? LayoutUpdated;

    public string? Name
    {
        get => AutomationId;
        set => AutomationId = value;
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ComboBox), 1.0);

    public static readonly BindableProperty UnselectedTextProperty =
        BindableProperty.Create(nameof(UnselectedText), typeof(string), typeof(ComboBox), string.Empty);

    public string UnselectedText
    {
        get => (string)GetValue(UnselectedTextProperty);
        set => SetValue(UnselectedTextProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
    public static readonly BindableProperty HeaderProperty =
        BindableProperty.Create(nameof(Header), typeof(object), typeof(ComboBox), null);

    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }
    public static readonly BindableProperty TagProperty =
        BindableProperty.Create(nameof(Tag), typeof(object), typeof(ComboBox), null);

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }
    public static readonly BindableProperty IsDropDownOpenProperty =
        BindableProperty.Create(nameof(IsDropDownOpen), typeof(bool), typeof(ComboBox), false);

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ComboBox), null, propertyChanged: OnItemsSourceChanged);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }
    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ComboBox), null, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public object? SelectedValue
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(ComboBox), null);

    public IList<object> Items => _items;

    public IList<SelectorOption> Options
    {
        get => (IList<SelectorOption>)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }
    public static readonly BindableProperty OptionsProperty =
        BindableProperty.Create(nameof(Options), typeof(IList<SelectorOption>), typeof(ComboBox), null);

    public SelectorOption? SelectedOption
    {
        get => (SelectorOption?)GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }
    public static readonly BindableProperty SelectedOptionProperty =
        BindableProperty.Create(nameof(SelectedOption), typeof(SelectorOption?), typeof(ComboBox), null,
            propertyChanged: (b, _, _) => (b as ComboBox)?.RaiseSelectionChanged());

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(ComboBox), -1, BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ComboBox), 14.0);

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }
    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ComboBox), null);

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, default!);

    public ComboBox()
    {
        Options = new ObservableCollection<SelectorOption>();
        Loaded += (_, _) => LayoutUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is ComboBox comboBox)
        {
            comboBox.SyncItems(newValue as IEnumerable);
        }
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is ComboBox comboBox)
        {
            comboBox.SyncSelectedIndexFromItem(newValue);
            comboBox.RaiseSelectionChanged();
        }
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        _ = oldValue;

        if (bindable is ComboBox comboBox)
        {
            comboBox.SyncSelectedItemFromIndex(newValue is int index ? index : -1);
            comboBox.RaiseSelectionChanged();
        }
    }

    private void SyncItems(IEnumerable? source)
    {
        _items.Clear();
        Options.Clear();

        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            _items.Add(item!);

            if (item is SelectorOption option)
            {
                Options.Add(option);
            }
            else
            {
                Options.Add(new SelectorOption(item?.ToString() ?? string.Empty, string.Empty));
            }
        }

        if (SelectedIndex >= _items.Count)
        {
            SelectedIndex = -1;
        }
    }

    private void SyncSelectedIndexFromItem(object? item)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SelectedIndex = item == null ? -1 : _items.IndexOf(item);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SyncSelectedItemFromIndex(int index)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SelectedItem = index >= 0 && index < _items.Count ? _items[index] : null;
            if (SelectedItem is SelectorOption option)
            {
                SelectedOption = option;
            }
            else
            {
                SelectedOption = index >= 0 && index < Options.Count ? Options[index] : null;
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}