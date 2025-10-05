
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ToggleDropdown : ContentView
{
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(ToggleDropdown), false);

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

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(ToggleDropdown), new Thickness(1));

    public static readonly BindableProperty UnselectedTextProperty =
        BindableProperty.Create(nameof(UnselectedText), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string UnselectedText
    {
        get => (string)GetValue(UnselectedTextProperty);
        set => SetValue(UnselectedTextProperty, value);
    }

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
        BindableProperty.Create(nameof(SelectedOption), typeof(SelectorOption?), typeof(ToggleDropdown), null);

}