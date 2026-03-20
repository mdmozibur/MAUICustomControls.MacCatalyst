using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace MAUICustomControls.MacCatalyst.Controls;

[ContentProperty(nameof(Children))]
public sealed class InlineCollectionView : HorizontalStackLayout
{
	public static readonly BindableProperty SelectedIndexProperty =
		BindableProperty.Create(
			nameof(SelectedIndex),
			typeof(int),
			typeof(InlineCollectionView),
			-1,
			BindingMode.TwoWay,
			propertyChanged: OnSelectedIndexChanged);

	public static readonly BindableProperty SelectionModeProperty =
		BindableProperty.Create(
			nameof(SelectionMode),
			typeof(SelectionMode),
			typeof(InlineCollectionView),
			SelectionMode.Single);

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public SelectionMode SelectionMode
	{
		get => (SelectionMode)GetValue(SelectionModeProperty);
		set => SetValue(SelectionModeProperty, value);
	}

	public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

	public InlineCollectionView()
	{
		Spacing = 12;
	}

	protected override void OnChildAdded(Element child)
	{
		base.OnChildAdded(child);

		if (child is View view)
		{
			AttachTapRecognizer(view);
		}
	}

	private static void OnSelectedIndexChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (bindable is not InlineCollectionView view)
		{
			return;
		}

		var previousIndex = oldValue is int oldIndex ? oldIndex : -1;
		var currentIndex = newValue is int newIndex ? newIndex : -1;

		view.UpdateSelectedVisual(previousIndex, false);
		view.UpdateSelectedVisual(currentIndex, true);

		var previousSelection = previousIndex >= 0 && previousIndex < view.Children.Count
			? new object[] { view.Children[previousIndex] }
			: Array.Empty<object>();
		var currentSelection = currentIndex >= 0 && currentIndex < view.Children.Count
			? new object[] { view.Children[currentIndex] }
			: Array.Empty<object>();

		view.SelectionChanged?.Invoke(view, new SelectionChangedEventArgs(previousSelection, currentSelection));
	}

	private void AttachTapRecognizer(View view)
	{
		if (view.GestureRecognizers.OfType<TapGestureRecognizer>().Any(recognizer => recognizer.CommandParameter == view))
		{
			return;
		}

		var tapRecognizer = new TapGestureRecognizer
		{
			CommandParameter = view,
			Command = new Command(() =>
			{
				if (SelectionMode == SelectionMode.None)
				{
					return;
				}

				var index = GetChildIndex(view);
				if (index >= 0)
				{
					SelectedIndex = index;
				}
			})
		};

		view.GestureRecognizers.Add(tapRecognizer);
	}

	private int GetChildIndex(View view)
	{
		for (var index = 0; index < Children.Count; index++)
		{
			if (ReferenceEquals(Children[index], view))
			{
				return index;
			}
		}

		return -1;
	}

	private void UpdateSelectedVisual(int index, bool isSelected)
	{
		if (index < 0 || index >= Children.Count)
		{
			return;
		}

		if (Children[index] is not View child)
		{
			return;
		}

		child.Opacity = isSelected ? 1.0 : 0.92;
	}
}