
namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class CheckBox : ContentView
{
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(CheckBox), false, propertyChanged: OnIsCheckedChanged);

    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;
    public event EventHandler? Checked;
    public event EventHandler? Unchecked;

    public string? Name
    {
        get => AutomationId;
        set => AutomationId = value;
    }

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

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(CheckBox), 14d);

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

    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(SolidColorBrush), typeof(CheckBox), Brush.DodgerBlue);

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CheckBox checkBox || newValue is not bool isChecked)
        {
            return;
        }

        checkBox.CheckedChanged?.Invoke(checkBox, new CheckedChangedEventArgs(isChecked));
        if (isChecked)
        {
            checkBox.Checked?.Invoke(checkBox, EventArgs.Empty);
        }
        else
        {
            checkBox.Unchecked?.Invoke(checkBox, EventArgs.Empty);
        }
    }
}