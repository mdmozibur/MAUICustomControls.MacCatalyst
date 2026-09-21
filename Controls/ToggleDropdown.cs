
using System.Collections.ObjectModel;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed partial class ToggleDropdown : ContentView
{
    private bool _nextToggleIsUserInitiated;
    private bool _nextSelectionIsUserInitiated;

    public event EventHandler<ToggleDropdownToggleChangedEventArgs>? Toggled;
    public event EventHandler? Checked;
    public event EventHandler<ToggleDropdownSelectionChangedEventArgs>? SelectionChanged;

    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(ToggleDropdown), false, BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    // Accent used for the icon and label while the dropdown is checked (the UWP "Checked" state).
    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }
    public static readonly BindableProperty TintColorProperty =
        BindableProperty.Create(nameof(TintColor), typeof(Color), typeof(ToggleDropdown), Colors.DodgerBlue);

    // Icon and label colour while unchecked. Null follows the system label colour, so it tracks light/dark.
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ToggleDropdown), null);

    // Null follows the system separator colour.
    public Color? BorderColor
    {
        get => (Color?)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }
    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(ToggleDropdown), null);

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(ToggleDropdown), 0.0);

    // Small marker drawn at the trailing edge while checked, signalling that a click opens the
    // option menu. Empty glyph/font falls back to an SF Symbol chevron.
    public string DropdownIndicatorGlyph
    {
        get => (string)GetValue(DropdownIndicatorGlyphProperty);
        set => SetValue(DropdownIndicatorGlyphProperty, value);
    }
    public static readonly BindableProperty DropdownIndicatorGlyphProperty =
        BindableProperty.Create(nameof(DropdownIndicatorGlyph), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string DropdownIndicatorFontFamily
    {
        get => (string)GetValue(DropdownIndicatorFontFamilyProperty);
        set => SetValue(DropdownIndicatorFontFamilyProperty, value);
    }
    public static readonly BindableProperty DropdownIndicatorFontFamilyProperty =
        BindableProperty.Create(nameof(DropdownIndicatorFontFamily), typeof(string), typeof(ToggleDropdown), string.Empty);

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
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(ToggleDropdown), 0.0);

    public static readonly BindableProperty UnselectedTextProperty =
        BindableProperty.Create(nameof(UnselectedText), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string UnselectedText
    {
        get => (string)GetValue(UnselectedTextProperty);
        set => SetValue(UnselectedTextProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(ToggleDropdown), 2.0);

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ToggleDropdown), 12.0);

    public StackOrientation Orientation
    {
        get => (StackOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public static readonly BindableProperty OrientationProperty =
        BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(ToggleDropdown), StackOrientation.Vertical);

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

    public bool ChangeUnselectedTextOnSelectionChange
    {
        get => (bool)GetValue(ChangeUnselectedTextOnSelectionChangeProperty);
        set => SetValue(ChangeUnselectedTextOnSelectionChangeProperty, value);
    }
    public static readonly BindableProperty ChangeUnselectedTextOnSelectionChangeProperty =
        BindableProperty.Create(nameof(ChangeUnselectedTextOnSelectionChange), typeof(bool), typeof(ToggleDropdown), true);

    public bool IsActionMenu
    {
        get => (bool)GetValue(IsActionMenuProperty);
        set => SetValue(IsActionMenuProperty, value);
    }
    public static readonly BindableProperty IsActionMenuProperty =
        BindableProperty.Create(nameof(IsActionMenu), typeof(bool), typeof(ToggleDropdown), false);

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }
    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }
    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(nameof(IconFontFamily), typeof(string), typeof(ToggleDropdown), string.Empty);

    public string SystemIconName
    {
        get => (string)GetValue(SystemIconNameProperty);
        set => SetValue(SystemIconNameProperty, value);
    }
    public static readonly BindableProperty SystemIconNameProperty =
        BindableProperty.Create(nameof(SystemIconName), typeof(string), typeof(ToggleDropdown), string.Empty);

    public ToggleDropdown()
    {
        Options = new ObservableCollection<SelectorOption>();
    }

    internal void SetSelectedOptionFromCompatibility(SelectorOption? selectedOption, bool isProgrammatic)
    {
        _nextSelectionIsUserInitiated = !isProgrammatic;
        SelectedOption = selectedOption;
    }

    internal void MarkNextToggleAsUserInitiated()
    {
        _nextToggleIsUserInitiated = true;
    }

    internal void MarkNextSelectionAsUserInitiated()
    {
        _nextSelectionIsUserInitiated = true;
    }

    partial void OnIsCheckedChangedPartial(bool isProgrammatic);

    partial void OnSelectedOptionChangedPartial(bool isProgrammatic);

    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ToggleDropdown toggleDropdown || newValue is not bool isChecked)
        {
            return;
        }

        var isProgrammatic = !toggleDropdown._nextToggleIsUserInitiated;
        toggleDropdown._nextToggleIsUserInitiated = false;
        toggleDropdown.Toggled?.Invoke(toggleDropdown, new ToggleDropdownToggleChangedEventArgs(isProgrammatic));
        if (isChecked)
        {
            toggleDropdown.Checked?.Invoke(toggleDropdown, EventArgs.Empty);
        }

        toggleDropdown.OnIsCheckedChangedPartial(isProgrammatic);
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

        var isProgrammatic = !toggleDropdown._nextSelectionIsUserInitiated;
        toggleDropdown._nextSelectionIsUserInitiated = false;
        toggleDropdown.SelectionChanged?.Invoke(toggleDropdown, new ToggleDropdownSelectionChangedEventArgs(isProgrammatic));
        toggleDropdown.OnSelectedOptionChangedPartial(isProgrammatic);
    }

}

public sealed class ToggleDropdownToggleChangedEventArgs : EventArgs
{
    public ToggleDropdownToggleChangedEventArgs(bool isProgrammatic)
    {
        IsProgrammatic = isProgrammatic;
    }

    public bool IsProgrammatic { get; }
}

public sealed class ToggleDropdownSelectionChangedEventArgs : EventArgs
{
    public ToggleDropdownSelectionChangedEventArgs(bool isProgrammatic)
    {
        IsProgrammatic = isProgrammatic;
    }

    public bool IsProgrammatic { get; }
}
