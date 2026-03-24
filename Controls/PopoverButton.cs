using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace MAUICustomControls.MacCatalyst.Controls;

[ContentProperty(nameof(Content))]
public sealed class PopoverButton : ContentView
{
	private object? _contentModel;

	public static readonly BindableProperty PopoverContentProperty =
		BindableProperty.Create(nameof(PopoverContent), typeof(View), typeof(PopoverButton), null);

	public View? PopoverContent
	{
		get => (View?)GetValue(PopoverContentProperty);
		set => SetValue(PopoverContentProperty, value);
	}

	public static readonly BindableProperty FlyoutProperty =
		BindableProperty.Create(nameof(Flyout), typeof(FlyoutBase), typeof(PopoverButton), null, propertyChanged: OnFlyoutChanged);

	public FlyoutBase? Flyout
	{
		get => (FlyoutBase?)GetValue(FlyoutProperty);
		set => SetValue(FlyoutProperty, value);
	}

	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(PopoverButton), string.Empty, propertyChanged: OnButtonFaceChanged);

	public string? Text
	{
		get => (string?)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly BindableProperty FontSizeProperty =
		BindableProperty.Create(nameof(FontSize), typeof(double), typeof(PopoverButton), 14d, propertyChanged: OnButtonFaceChanged);

	public double FontSize
	{
		get => (double)GetValue(FontSizeProperty);
		set => SetValue(FontSizeProperty, value);
	}

	public static readonly BindableProperty FontFamilyProperty =
		BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(PopoverButton), null, propertyChanged: OnButtonFaceChanged);

	public string? FontFamily
	{
		get => (string?)GetValue(FontFamilyProperty);
		set => SetValue(FontFamilyProperty, value);
	}

	public static readonly BindableProperty FontAttributesProperty =
		BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(PopoverButton), FontAttributes.None, propertyChanged: OnButtonFaceChanged);

	public FontAttributes FontAttributes
	{
		get => (FontAttributes)GetValue(FontAttributesProperty);
		set => SetValue(FontAttributesProperty, value);
	}

	public static readonly BindableProperty TextColorProperty =
		BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(PopoverButton), Colors.Black, propertyChanged: OnButtonFaceChanged);

	public Color TextColor
	{
		get => (Color)GetValue(TextColorProperty);
		set => SetValue(TextColorProperty, value);
	}

	public static readonly BindableProperty ImageSourceProperty =
		BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(PopoverButton), null, propertyChanged: OnButtonFaceChanged);

	public ImageSource? ImageSource
	{
		get => (ImageSource?)GetValue(ImageSourceProperty);
		set => SetValue(ImageSourceProperty, value);
	}

	public static readonly BindableProperty ContentLayoutProperty =
		BindableProperty.Create(nameof(ContentLayout), typeof(string), typeof(PopoverButton), null);

	public string? ContentLayout
	{
		get => (string?)GetValue(ContentLayoutProperty);
		set => SetValue(ContentLayoutProperty, value);
	}
	public static readonly BindableProperty BorderColorProperty =
		BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(PopoverButton), Colors.Gray);

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
	BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(PopoverButton), 1.0);
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
	public static readonly BindableProperty PopoverDirectionProperty =
            BindableProperty.Create(nameof(PopoverDirection), typeof(PopoverDirection), typeof(PopoverButton), PopoverDirection.Auto);

	public PopoverDirection PopoverDirection
	{
		get => (PopoverDirection)GetValue(PopoverDirectionProperty);
		set => SetValue(PopoverDirectionProperty, value);
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

	internal Action? HidePopoverAction { get; set; }

	public void HidePopover()
	{
		HidePopoverAction?.Invoke();
	}

	internal View? GetPresentedContent()
	{
		return Flyout?.BuildView() ?? PopoverContent;
	}

	internal void RaiseClicked()
	{
		Clicked?.Invoke(this, EventArgs.Empty);
	}

	private static void OnFlyoutChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (oldValue is FlyoutBase oldFlyout)
		{
			oldFlyout.AttachOwner(null);
		}

		if (bindable is PopoverButton button && newValue is FlyoutBase newFlyout)
		{
			newFlyout.AttachOwner(button);
		}
	}

	private static void OnButtonFaceChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (bindable is PopoverButton button)
		{
			button.UpdateDisplayedContent();
		}
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