
namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class CheckBox : ContentView
{
    
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(CheckBox), false);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ToggleDropdown), 1.0);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(CheckBox), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(SolidColorBrush), typeof(CheckBox), Brush.DodgerBlue);
}