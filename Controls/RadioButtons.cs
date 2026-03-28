using System.Collections.ObjectModel;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class RadioButtonsSelectionChangedEventArgs : EventArgs
{
	public RadioButtonsSelectionChangedEventArgs(IReadOnlyList<object> removedItems, IReadOnlyList<object> addedItems)
	{
		RemovedItems = removedItems;
		AddedItems = addedItems;
		PreviousSelection = removedItems;
		CurrentSelection = addedItems;
	}

	public IReadOnlyList<object> RemovedItems { get; }

	public IReadOnlyList<object> AddedItems { get; }

	public IReadOnlyList<object> PreviousSelection { get; }

	public IReadOnlyList<object> CurrentSelection { get; }
}

[ContentProperty(nameof(Items))]
public sealed class RadioButtons : VerticalStackLayout
{
	public static readonly BindableProperty HeaderProperty =
		BindableProperty.Create(nameof(Header), typeof(string), typeof(RadioButtons), string.Empty,
			propertyChanged: OnHeaderChanged);

	public static readonly BindableProperty SelectedIndexProperty =
		BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(RadioButtons), -1,
			BindingMode.TwoWay,
			propertyChanged: OnSelectedIndexChanged);

	public static readonly BindableProperty MaxColumnsProperty =
		BindableProperty.Create(nameof(MaxColumns), typeof(int), typeof(RadioButtons), 1);

	private readonly ObservableCollection<RadioButton> _items = new();
	private readonly Label _headerLabel;
	private Grid? _itemsGrid;
	private bool _layoutBuilt;
	private bool _updatingSelection;

	public event EventHandler<RadioButtonsSelectionChangedEventArgs>? SelectionChanged;

	public string Header
	{
		get => (string)GetValue(HeaderProperty);
		set => SetValue(HeaderProperty, value);
	}

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public int MaxColumns
	{
		get => (int)GetValue(MaxColumnsProperty);
		set => SetValue(MaxColumnsProperty, value);
	}

	public object? SelectedItem =>
		SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;

	public ObservableCollection<RadioButton> Items => _items;

	public RadioButtons()
	{
		Spacing = 4;
		_headerLabel = new Label
		{
			IsVisible = false,
			FontSize = 14,
			Margin = new Thickness(0, 0, 0, 2),
		};
		base.Children.Add(_headerLabel);

		Loaded += OnLoaded;
	}

	private void OnLoaded(object? sender, EventArgs e)
	{
		Loaded -= OnLoaded;
		BuildLayout();
	}

	private void BuildLayout()
	{
		if (_layoutBuilt)
			return;

		_layoutBuilt = true;

		int maxCols = Math.Max(1, MaxColumns);

		_itemsGrid = new Grid
		{
			ColumnSpacing = 8,
			RowSpacing = 4,
		};

		for (int c = 0; c < maxCols; c++)
			_itemsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

		int rows = (_items.Count + maxCols - 1) / maxCols;
		for (int r = 0; r < rows; r++)
			_itemsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

		string groupName = $"RadioButtons_{GetHashCode()}";

		for (int i = 0; i < _items.Count; i++)
		{
			var rb = _items[i];
			rb.GroupName = groupName;
			rb.CheckedChanged += OnRadioButtonCheckedChanged;

			int row = i / maxCols;
			int col = i % maxCols;
			Grid.SetRow(rb, row);
			Grid.SetColumn(rb, col);
			_itemsGrid.Children.Add(rb);
		}

		base.Children.Add(_itemsGrid);

		if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
		{
			_updatingSelection = true;
			_items[SelectedIndex].IsChecked = true;
			_updatingSelection = false;
		}
	}

	private void OnRadioButtonCheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		if (_updatingSelection || sender is not RadioButton rb || !e.Value)
			return;

		int newIndex = _items.IndexOf(rb);
		if (newIndex < 0 || newIndex == SelectedIndex)
			return;

		var previousItem = SelectedItem;
		_updatingSelection = true;
		SelectedIndex = newIndex;
		_updatingSelection = false;

		var previousSelection = previousItem != null
			? new List<object> { previousItem }
			: new List<object>();
		var currentSelection = new List<object> { rb };
		SelectionChanged?.Invoke(this, new RadioButtonsSelectionChangedEventArgs(previousSelection, currentSelection));
	}

	private static void OnHeaderChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is RadioButtons control && newValue is string header)
		{
			control._headerLabel.Text = header;
			control._headerLabel.IsVisible = !string.IsNullOrWhiteSpace(header);
		}
	}

	private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not RadioButtons control || newValue is not int index)
			return;

		if (control._updatingSelection || !control._layoutBuilt)
			return;

		control._updatingSelection = true;

		if (index >= 0 && index < control._items.Count)
		{
			control._items[index].IsChecked = true;
		}

		control._updatingSelection = false;

		var previousItem = oldValue is int oldIdx && oldIdx >= 0 && oldIdx < control._items.Count
			? control._items[oldIdx]
			: null;
		var currentItem = control.SelectedItem;

		var previousSelection = previousItem != null
			? new List<object> { previousItem }
			: new List<object>();
		var currentSelection = currentItem != null
			? new List<object> { currentItem }
			: new List<object>();
		control.SelectionChanged?.Invoke(control, new RadioButtonsSelectionChangedEventArgs(previousSelection, currentSelection));
	}

	public void SetSelectedIndex(int value) => SelectedIndex = value;

	public int GetSelectedIndex() => SelectedIndex;
}
