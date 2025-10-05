using System.Collections.ObjectModel;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ComboBox : ContentView
{
    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(ComboBox), new Thickness(1));

    public static readonly BindableProperty UnselectedTextProperty =
        BindableProperty.Create(nameof(UnselectedText), typeof(string), typeof(ComboBox), string.Empty);

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
        BindableProperty.Create(nameof(Options), typeof(IList<SelectorOption>), typeof(ComboBox), null);

    public SelectorOption? SelectedOption
    {
        get => (SelectorOption?)GetValue(SelectedOptionProperty);
        set => SetValue(SelectedOptionProperty, value);
    }
    public static readonly BindableProperty SelectedOptionProperty =
        BindableProperty.Create(nameof(SelectedOption), typeof(SelectorOption?), typeof(ComboBox), null);

    public ComboBox()
    {
        Options = new ObservableCollection<SelectorOption>();
    }
}