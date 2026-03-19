namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ToggleButton : View
{
    public event EventHandler? Toggled;

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(ToggleButton), false, BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ToggleButton), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ToggleButton), 14d);

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }
    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(ToggleButton), new Thickness(14, 10));

    public LayoutOptions HorizontalContentAlignment
    {
        get => (LayoutOptions)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }
    public static readonly BindableProperty HorizontalContentAlignmentProperty =
        BindableProperty.Create(nameof(HorizontalContentAlignment), typeof(LayoutOptions), typeof(ToggleButton), LayoutOptions.Center);

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(ToggleButton), new Thickness(1));

    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }
    public static readonly BindableProperty BorderBrushProperty =
        BindableProperty.Create(nameof(BorderBrush), typeof(Brush), typeof(ToggleButton), null);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }
    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(ToggleButton), string.Empty);

    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(SolidColorBrush), typeof(ToggleButton), Brush.DodgerBlue);

    public double ImageSpacing
    {
        get => (double)GetValue(ImageSpacingProperty);
        set => SetValue(ImageSpacingProperty, value);
    }
    public static readonly BindableProperty ImageSpacingProperty =
        BindableProperty.Create(nameof(ImageSpacing), typeof(double), typeof(ToggleButton), 6.0);

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ToggleButton toggleButton || newValue is not bool)
        {
            return;
        }

        toggleButton.Toggled?.Invoke(toggleButton, EventArgs.Empty);
    }
}