using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace MAUICustomControls.MacCatalyst.Controls;

[ContentProperty(nameof(Content))]
public sealed class ContentButton : ContentView
{
	private object? _contentModel;

	public ContentButton()
	{
		var tapGestureRecognizer = new TapGestureRecognizer();
		tapGestureRecognizer.Tapped += OnTapped;
		GestureRecognizers.Add(tapGestureRecognizer);
	}

	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(ContentButton), string.Empty, propertyChanged: OnButtonFaceChanged);

	public string? Text
	{
		get => (string?)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly BindableProperty FontSizeProperty =
		BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ContentButton), 14d, propertyChanged: OnButtonFaceChanged);

	public double FontSize
	{
		get => (double)GetValue(FontSizeProperty);
		set => SetValue(FontSizeProperty, value);
	}

	public static readonly BindableProperty FontFamilyProperty =
		BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(ContentButton), null, propertyChanged: OnButtonFaceChanged);

	public string? FontFamily
	{
		get => (string?)GetValue(FontFamilyProperty);
		set => SetValue(FontFamilyProperty, value);
	}

	public static readonly BindableProperty FontAttributesProperty =
		BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(ContentButton), FontAttributes.None, propertyChanged: OnButtonFaceChanged);

	public FontAttributes FontAttributes
	{
		get => (FontAttributes)GetValue(FontAttributesProperty);
		set => SetValue(FontAttributesProperty, value);
	}

	public static readonly BindableProperty TextColorProperty =
		BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ContentButton), Colors.Black, propertyChanged: OnButtonFaceChanged);

	public Color TextColor
	{
		get => (Color)GetValue(TextColorProperty);
		set => SetValue(TextColorProperty, value);
	}

	public static readonly BindableProperty ImageSourceProperty =
		BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(ContentButton), null, propertyChanged: OnButtonFaceChanged);

	public ImageSource? ImageSource
	{
		get => (ImageSource?)GetValue(ImageSourceProperty);
		set => SetValue(ImageSourceProperty, value);
	}

	public static readonly BindableProperty ContentLayoutProperty =
		BindableProperty.Create(nameof(ContentLayout), typeof(string), typeof(ContentButton), null);

	public string? ContentLayout
	{
		get => (string?)GetValue(ContentLayoutProperty);
		set => SetValue(ContentLayoutProperty, value);
	}

	public static readonly BindableProperty BorderColorProperty =
		BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(ContentButton), Colors.Gray);

	public Color BorderColor
	{
		get => (Color)GetValue(BorderColorProperty);
		set => SetValue(BorderColorProperty, value);
	}

	public Color BorderBrush
	{
		get => BorderColor;
		set => BorderColor = value;
	}

	public static readonly BindableProperty BorderWidthProperty =
		BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(ContentButton), 1.0);

	public double BorderWidth
	{
		get => (double)GetValue(BorderWidthProperty);
		set => SetValue(BorderWidthProperty, value);
	}

	public double BorderThickness
	{
		get => BorderWidth;
		set => BorderWidth = value;
	}

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ContentButton), null);

	public ICommand? Command
	{
		get => (ICommand?)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ContentButton), null);

	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	public new object? Content
	{
		get => _contentModel;
		set
		{
			_contentModel = value;
			UpdateDisplayedContent();
		}
	}

	public event EventHandler? Clicked;

	private static void OnButtonFaceChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (bindable is ContentButton button)
		{
			button.UpdateDisplayedContent();
		}
	}

	private void OnTapped(object? sender, TappedEventArgs e)
	{
		_ = sender;
		_ = e;

		if (!IsEnabled)
		{
			return;
		}

		if (Command?.CanExecute(CommandParameter) == true)
		{
			Command.Execute(CommandParameter);
		}

		Clicked?.Invoke(this, EventArgs.Empty);
	}

	private void UpdateDisplayedContent()
	{
		if (_contentModel is View view)
		{
			base.Content = view;
			return;
		}

		if (_contentModel is string text)
		{
			base.Content = CreateLabel(text);
			return;
		}

		if (ImageSource == null && string.IsNullOrWhiteSpace(Text))
		{
			base.Content = null;
			return;
		}

		var layout = new HorizontalStackLayout
		{
			Spacing = 5,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
		};

		if (ImageSource != null)
		{
			layout.Children.Add(new Image
			{
				Source = ImageSource,
				WidthRequest = FontSize + 2,
				HeightRequest = FontSize + 2,
				VerticalOptions = LayoutOptions.Center,
			});
		}

		if (!string.IsNullOrWhiteSpace(Text))
		{
			layout.Children.Add(CreateLabel(Text));
		}

		base.Content = layout;
	}

	private Label CreateLabel(string text)
	{
		return new Label
		{
			Text = text,
			FontAttributes = FontAttributes,
			FontSize = FontSize,
			FontFamily = FontFamily,
			TextColor = TextColor,
			VerticalOptions = LayoutOptions.Center,
		};
	}
}