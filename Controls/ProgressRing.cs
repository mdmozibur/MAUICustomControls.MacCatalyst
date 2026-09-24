namespace MAUICustomControls.MacCatalyst.Controls;

// WinUI ProgressRing: an indeterminate spinner by default, or a determinate arc showing
// (Value - Minimum) / (Maximum - Minimum). Like WinUI, an inactive ring keeps its layout slot but
// draws nothing, and it is active unless told otherwise.
public sealed class ProgressRing : View
{
    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive),
        typeof(bool),
        typeof(ProgressRing),
        true);

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

    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(
        nameof(Minimum),
        typeof(double),
        typeof(ProgressRing),
        0d);

    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(
        nameof(Maximum),
        typeof(double),
        typeof(ProgressRing),
        100d);

    // Arc colour (UWP Foreground). Null follows the system accent colour.
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(ProgressRing),
        null);

    // Track behind a determinate arc (UWP Background). Null uses a subtle system fill.
    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor),
        typeof(Color),
        typeof(ProgressRing),
        null);

    // WinUI's ProgressRing style centres the ring in its slot at its 32 pt default size; a MAUI view
    // would fill the slot instead, and the ring drew as large as the export preview. XAML that sets
    // an alignment still overrides these.
    public ProgressRing()
    {
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
    }

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

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color? TrackColor
    {
        get => (Color?)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public double Fraction
    {
        get
        {
            var range = Maximum - Minimum;
            return range <= 0 ? 0 : Math.Clamp((Value - Minimum) / range, 0, 1);
        }
    }
}
