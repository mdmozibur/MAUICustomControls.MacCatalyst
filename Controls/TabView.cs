using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

public class TabViewItem : ContentView
{
	public static readonly BindableProperty HeaderProperty =
		BindableProperty.Create(nameof(Header), typeof(string), typeof(TabViewItem), string.Empty,
			propertyChanged: OnHeaderChanged);

	public string Header
	{
		get => (string)GetValue(HeaderProperty);
		set => SetValue(HeaderProperty, value);
	}

	internal Action<TabViewItem>? HeaderChangedCallback { get; set; }

	private static void OnHeaderChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is TabViewItem item)
		{
			item.HeaderChangedCallback?.Invoke(item);
		}
	}
}

[ContentProperty(nameof(Items))]
public class TabView : Grid
{
	public static readonly BindableProperty SelectedIndexProperty =
		BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(TabView), 0,
			BindingMode.TwoWay,
			propertyChanged: OnSelectedIndexChanged);

	private readonly ObservableCollection<TabViewItem> _items = new();
	private readonly HorizontalStackLayout _headerRow;
	private readonly string _focusGroup;
	private bool _layoutBuilt;

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public ObservableCollection<TabViewItem> Items => _items;

	public TabView()
	{
		RowSpacing = 0;
		RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

		_headerRow = new HorizontalStackLayout
		{
			Spacing = 0,
		};
		_focusGroup = AccessibleItemFocus.GroupFor(this);
		Grid.SetRow((BindableObject)_headerRow, 0);
		base.Children.Add(_headerRow);

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

		_headerRow.Children.Clear();

		for (int i = 0; i < _items.Count; i++)
		{
			var item = _items[i];
			var index = i;

			item.HeaderChangedCallback = OnItemHeaderChanged;

			var headerLabel = new Label
			{
				Text = item.Header ?? $"Tab {i + 1}",
				Padding = new Thickness(12, 8),
				VerticalTextAlignment = TextAlignment.Center,
			};

			// A tab to the keyboard and VoiceOver: one Tab stop for the strip, ← and → move
			// between tabs, Space/Return select the focused one.
			var header = new AccessibleItemView
			{
				Role = AccessibleItemRole.Tab,
				FocusGroup = _focusGroup,
				Content = headerLabel,
			};
			header.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(() => SelectedIndex = index),
			});
			header.Invoked += (_, _) => SelectedIndex = index;
			header.MoveRequested += (_, args) => args.Handled = MoveSelection(index + args.Delta);

			_headerRow.Children.Add(header);

			Grid.SetRow((BindableObject)item, 1);
			base.Children.Add(item);
			item.IsVisible = (i == SelectedIndex);
		}

		_layoutBuilt = true;
		UpdateSelection();
	}

	private bool MoveSelection(int index)
	{
		if (index < 0 || index >= _items.Count || index >= _headerRow.Children.Count)
		{
			return false;
		}

		SelectedIndex = index;
		AccessibleItemFocus.MoveTo(_headerRow.Children[index]);
		return true;
	}

	private void OnItemHeaderChanged(TabViewItem item)
	{
		var idx = _items.IndexOf(item);
		if (idx >= 0 && idx < _headerRow.Children.Count && _headerRow.Children[idx] is AccessibleItemView { Content: Label lbl })
		{
			lbl.Text = item.Header;
		}
	}

	private void UpdateSelection()
	{
		if (!_layoutBuilt)
			return;

		for (int i = 0; i < _items.Count; i++)
		{
			_items[i].IsVisible = (i == SelectedIndex);
		}

		UpdateHeaderStyles();
	}

	private void UpdateHeaderStyles()
	{
		for (int i = 0; i < _headerRow.Children.Count; i++)
		{
			if (_headerRow.Children[i] is AccessibleItemView { Content: Label lbl } header)
			{
				bool selected = (i == SelectedIndex);
				header.IsSelected = selected;
				lbl.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
				lbl.Opacity = selected ? 1.0 : 0.6;
				lbl.TextDecorations = selected ? TextDecorations.Underline : TextDecorations.None;
			}
		}
	}

	private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is TabView tabView)
		{
			tabView.UpdateSelection();
		}
	}

	public void SetSelectedIndex(int value) => SelectedIndex = value;

	public int GetSelectedIndex() => SelectedIndex;
}