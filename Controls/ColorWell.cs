using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A color swatch that opens the system color picker when clicked — the Mac counterpart of the
/// spectrum in UWP's ColorPicker. Backed by UIColorWell on Mac Catalyst.
/// </summary>
public sealed class ColorWell : View
{
	public static readonly BindableProperty SelectedColorProperty =
		BindableProperty.Create(nameof(SelectedColor), typeof(Color), typeof(ColorWell), Colors.Black, BindingMode.TwoWay,
			propertyChanged: (bindable, _, _) => ((ColorWell)bindable).SelectedColorChanged?.Invoke(bindable, EventArgs.Empty));

	public Color SelectedColor
	{
		get => (Color)GetValue(SelectedColorProperty);
		set => SetValue(SelectedColorProperty, value);
	}

	public static readonly BindableProperty SupportsAlphaProperty =
		BindableProperty.Create(nameof(SupportsAlpha), typeof(bool), typeof(ColorWell), false);

	public bool SupportsAlpha
	{
		get => (bool)GetValue(SupportsAlphaProperty);
		set => SetValue(SupportsAlphaProperty, value);
	}

	public static readonly BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(ColorWell), string.Empty);

	/// <summary>Title of the color picker the well opens.</summary>
	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	/// <summary>Raised whenever <see cref="SelectedColor"/> changes, from code or from the picker.</summary>
	public event EventHandler? SelectedColorChanged;
}
