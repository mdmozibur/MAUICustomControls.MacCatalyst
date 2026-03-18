namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ProgressRing : ActivityIndicator
{
    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive),
        typeof(bool),
        typeof(ProgressRing),
        true,
        propertyChanged: OnIsActiveChanged);

    public static readonly BindableProperty IsIndeterminateProperty = BindableProperty.Create(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(ProgressRing),
        true);

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(double),
        typeof(ProgressRing),
        0d);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(ProgressRing),
        null,
        propertyChanged: OnTextColorChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public ProgressRing()
    {
        IsRunning = IsActive;
    }

    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProgressRing progressRing && newValue is bool isActive)
        {
            progressRing.IsRunning = isActive;
        }
    }

    private static void OnTextColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProgressRing progressRing)
        {
            progressRing.Color = newValue as Color;
        }
    }
}
