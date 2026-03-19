
using System.Collections.ObjectModel;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ToggleDropdown : ContentView
{
    public event EventHandler? Toggled;
    public event EventHandler? Checked;
    public event EventHandler? SelectionChanged;

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(ToggleDropdown), false, BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }
    public static readonly BindableProperty TintColorProperty =
        BindableProperty.Create(nameof(TintColor), typeof(Color), typeof(ToggleDropdown), Colors.DodgerBlue);

    public LayoutOptions HorizontalContentAlignment
    {
        get => (LayoutOptions)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }
    public static readonly BindableProperty HorizontalContentAlignmentProperty =
        BindableProperty.Create(nameof(HorizontalContentAlignment), typeof(LayoutOptions), typeof(ToggleDropdown), LayoutOptions.Center);

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ToggleDropdown), 1.0);

    public static readonly BindableProperty UnselectedTextProperty =
        BindableProperty.Create(nameof(UnselectedText), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string UnselectedText
    {
        get => (string)GetValue(UnselectedTextProperty);
        set => SetValue(UnselectedTextProperty, value);
    }

    // Compatibility aliases for legacy SmartCad UWP property names.
    public string Text
    {
        get => UnselectedText;
        set => UnselectedText = value;
    }

    public string Label
    {
        get => UnselectedText;
        set => UnselectedText = value;
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(ToggleDropdown), 2.0);

    public double IconFontSize
    {
        get => (double)GetValue(IconFontSizeProperty);
        set => SetValue(IconFontSizeProperty, value);
    }
    public static readonly BindableProperty IconFontSizeProperty =
        BindableProperty.Create(nameof(IconFontSize), typeof(double), typeof(ToggleDropdown), 14.0);

    public FontAttributes IconFontWeight
    {
        get => (FontAttributes)GetValue(IconFontWeightProperty);
        set => SetValue(IconFontWeightProperty, value);
    }
    public static readonly BindableProperty IconFontWeightProperty =
        BindableProperty.Create(nameof(IconFontWeight), typeof(FontAttributes), typeof(ToggleDropdown), FontAttributes.None);

    public IList<SelectorOption> Options
    {
        get => (IList<SelectorOption>)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }
    public static readonly BindableProperty OptionsProperty =
        BindableProperty.Create(nameof(Options), typeof(IList<SelectorOption>), typeof(ToggleDropdown), null);

    public SelectorOption? SelectedOption
    {
        get => (SelectorOption?)GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }
    public static readonly BindableProperty SelectedOptionProperty =
        BindableProperty.Create(nameof(SelectedOption), typeof(SelectorOption?), typeof(ToggleDropdown), null, BindingMode.TwoWay, propertyChanged: OnSelectedOptionChanged);

    public SelectorOption? SelectedItem
    {
        get => SelectedOption;
        set => SelectedOption = value;
    }

    public ToggleDropdown()
    {
        Options = new ObservableCollection<SelectorOption>();
    }

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ToggleDropdown toggleDropdown || newValue is not bool isChecked)
        {
            return;
        }

        toggleDropdown.Toggled?.Invoke(toggleDropdown, EventArgs.Empty);
        if (isChecked)
        {
            toggleDropdown.Checked?.Invoke(toggleDropdown, EventArgs.Empty);
        }
    }

    private static void OnSelectedOptionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ToggleDropdown toggleDropdown)
        {
            return;
        }

        if (Equals(oldValue, newValue))
        {
            return;
        }

        toggleDropdown.SelectionChanged?.Invoke(toggleDropdown, EventArgs.Empty);
    }

}