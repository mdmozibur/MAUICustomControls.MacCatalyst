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

			headerLabel.GestureRecognizers.Add(new TapGestureRecognizer
			{
				Command = new Command(() => SelectedIndex = index),
			});

			_headerRow.Children.Add(headerLabel);

			Grid.SetRow((BindableObject)item, 1);
			base.Children.Add(item);
			item.IsVisible = (i == SelectedIndex);
		}

		_layoutBuilt = true;
		UpdateSelection();
	}

	private void OnItemHeaderChanged(TabViewItem item)
	{
		var idx = _items.IndexOf(item);
		if (idx >= 0 && idx < _headerRow.Children.Count && _headerRow.Children[idx] is Label lbl)
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
			if (_headerRow.Children[i] is Label lbl)
			{
				bool selected = (i == SelectedIndex);
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