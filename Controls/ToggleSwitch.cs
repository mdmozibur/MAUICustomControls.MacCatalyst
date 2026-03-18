namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ToggleSwitch : View
{
    public static readonly BindableProperty IsOnProperty =
        BindableProperty.Create(nameof(IsOn), typeof(bool), typeof(ToggleSwitch), false, BindingMode.TwoWay, propertyChanged: OnIsOnChanged);

    public event EventHandler? Toggled;

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ToggleSwitch), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ToggleSwitch), 14d);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty PaddingProperty =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(ToggleSwitch), default(Thickness));

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(SolidColorBrush), typeof(ToggleSwitch), Brush.Black);

    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly BindableProperty OnColorProperty =
        BindableProperty.Create(nameof(OnColor), typeof(Color), typeof(ToggleSwitch), Colors.DodgerBlue);

    public Color OnColor
    {
        get => (Color)GetValue(OnColorProperty);
        set => SetValue(OnColorProperty, value);
    }

    private static void OnIsOnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ToggleSwitch toggleSwitch || newValue is not bool)
        {
            return;
        }

        toggleSwitch.Toggled?.Invoke(toggleSwitch, EventArgs.Empty);
    }
}