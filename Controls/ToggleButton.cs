namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ToggleButton : View
{

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(ToggleButton), false);

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
}