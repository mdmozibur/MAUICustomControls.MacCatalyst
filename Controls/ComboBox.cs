using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class ComboBox : ContentView
{
    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }
    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ComboBox), 1.0);

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
        BindableProperty.Create(nameof(SelectedOption), typeof(SelectorOption?), typeof(ComboBox), null,
            propertyChanged: (b, _, _) => (b as ComboBox)?.RaiseSelectionChanged());

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(ComboBox), -1);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ComboBox), 14.0);

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }
    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ComboBox), null);

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(this, new SelectionChangedEventArgs([], []));

    public ComboBox()
    {
        Options = new ObservableCollection<SelectorOption>();
    }
}