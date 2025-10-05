namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class PopoverButton : ContentView
{
	public static readonly BindableProperty PopoverContentProperty =
		BindableProperty.Create(nameof(PopoverContent), typeof(View), typeof(PopoverButton), null);

	public View PopoverContent
	{
		get => (View)GetValue(PopoverContentProperty);
		set => SetValue(PopoverContentProperty, value);
	}
	public static readonly BindableProperty BorderColorProperty =
		BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(PopoverButton), Colors.Gray);

	public Color BorderColor
	{
		get => (Color)GetValue(BorderColorProperty);
		set => SetValue(BorderColorProperty, value);
	}

	public static readonly BindableProperty BorderWidthProperty =
	BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(PopoverButton), 1.0);
	public double BorderWidth
	{
		get => (double)GetValue(BorderWidthProperty);
		set => SetValue(BorderWidthProperty, value);
	}
	public static readonly BindableProperty PopoverDirectionProperty =
            BindableProperty.Create(nameof(PopoverDirection), typeof(PopoverDirection), typeof(PopoverButton), PopoverDirection.Auto);

	public PopoverDirection PopoverDirection
	{
		get => (PopoverDirection)GetValue(PopoverDirectionProperty);
		set => SetValue(PopoverDirectionProperty, value);
	}

	internal Action HidePopoverAction { get; set; }
	public PopoverButton()
	{
	}

	protected override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
	}
	public void HidePopover()
	{
		HidePopoverAction?.Invoke();
	}
}