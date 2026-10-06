using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>WinUI's ColorSpectrumShape. Only <see cref="Box"/> is drawn.</summary>
public enum ColorSpectrumShape
{
    Box,
    Ring,
}

/// <summary>
/// WinUI's ColorSpectrumComponents: which two of hue, saturation and value the spectrum shows.
/// Only <see cref="HueSaturation"/> is drawn (hue across, saturation from full at the top to none
/// at the bottom, value on the slider).
/// </summary>
public enum ColorSpectrumComponents
{
    HueValue,
    ValueHue,
    HueSaturation,
    SaturationHue,
    SaturationValue,
    ValueSaturation,
}

/// <summary>WinUI's ColorChangedEventArgs.</summary>
public sealed class ColorChangedEventArgs(Color oldColor, Color newColor) : EventArgs
{
    public Color OldColor { get; } = oldColor;

    public Color NewColor { get; } = newColor;
}

/// <summary>The handler of <see cref="ColorPicker.ColorChanged"/>.</summary>
public delegate void ColorChangedEventHandler(ColorPicker sender, ColorChangedEventArgs args);

/// <summary>
/// The hue and saturation field of <see cref="ColorPicker"/> (WinUI's ColorSpectrum as a box): the
/// user clicks or drags in it to choose both. Drawn natively: one cached hue-by-saturation image
/// under a black layer whose opacity is one minus <see cref="Value"/>, and a ring at the selection,
/// so a drag only moves a layer.
/// </summary>
public sealed class ColorSpectrum : View
{
    public static readonly BindableProperty HueProperty =
        BindableProperty.Create(nameof(Hue), typeof(double), typeof(ColorSpectrum), 0d);

    public static readonly BindableProperty SaturationProperty =
        BindableProperty.Create(nameof(Saturation), typeof(double), typeof(ColorSpectrum), 0d);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(ColorSpectrum), 1d);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(ColorSpectrum), 4d);

    /// <summary>Hue in degrees, 0 up to 360.</summary>
    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    /// <summary>Saturation, 0 to 1.</summary>
    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>Value (brightness), 0 to 1: how dark the whole field is.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>The user chose a point in the field; <see cref="Hue"/> and <see cref="Saturation"/> are already set.</summary>
    public event EventHandler? SelectionChangedByUser;

    /// <summary>Called by the platform view while the user clicks, drags, or moves the selection with the arrow keys.</summary>
    internal void SelectFromUser(double hue, double saturation)
    {
        Hue = Math.Clamp(hue, 0, 360);
        Saturation = Math.Clamp(saturation, 0, 1);
        SelectionChangedByUser?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The value slider of <see cref="ColorPicker"/> (WinUI's ColorPickerSlider for the third
/// dimension): a track from black to the chosen hue and saturation at full value.
/// </summary>
public sealed class ColorPickerSlider : View
{
    public static readonly BindableProperty HueProperty =
        BindableProperty.Create(nameof(Hue), typeof(double), typeof(ColorPickerSlider), 0d);

    public static readonly BindableProperty SaturationProperty =
        BindableProperty.Create(nameof(Saturation), typeof(double), typeof(ColorPickerSlider), 0d);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(ColorPickerSlider), 1d);

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>Value (brightness), 0 at the left end to 1 at the right.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The user moved the thumb; <see cref="Value"/> is already set.</summary>
    public event EventHandler? ValueChangedByUser;

    internal void SetValueFromUser(double value)
    {
        Value = Math.Clamp(value, 0, 1);
        ValueChangedByUser?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// WinUI's ColorPicker for the Mac, laid out as WinUI 2 lays out the vertical picker: the
/// hue-and-saturation spectrum (256 pt square) with the colour preview strip (44 pt) beside it,
/// the value slider under them, then the RGB/HSV selector with the hex box and the three channel
/// boxes with their names.
/// </summary>
/// <remarks>
/// What WinUI's control has and this one does not draw: the ring spectrum, spectrum components
/// other than hue and saturation, and alpha (slider and text box). Their properties exist so XAML
/// written for WinUI converts, and keep their values.
/// </remarks>
public sealed class ColorPicker : ContentView
{
    private const double SpectrumSize = 256;
    private const double PreviewWidth = 44;
    private const double Gap = 12;
    private const double InputWidth = 120;

    // WinUI's text boxes and combo box: 32 px tall, 14 px text.
    private const double InputHeight = 32;
    private const double InputFontSize = 14;

    private readonly ColorSpectrum _spectrum;
    private readonly ColorPickerSlider _slider;
    private readonly Border _previewBorder;
    private readonly BoxView _preview;
    private readonly BoxView _previousPreview;
    private readonly ComboBox _representation;
    private readonly Entry _hexBox;
    private readonly Entry[] _channelBoxes = new Entry[3];
    private readonly Label[] _channelLabels = new Label[3];
    private readonly Grid _textEntryGrid;

    // The selection as the user sees it. Kept beside Color because a colour does not say which hue
    // a grey has or which saturation black has: without it the ring would jump to the left edge
    // when a drag passes through the bottom row, or the slider reaches black.
    private double _hue;
    private double _saturation;
    private double _value = 1;
    private bool _updating;

    public ColorPicker()
    {
        _spectrum = new ColorSpectrum { WidthRequest = SpectrumSize, HeightRequest = SpectrumSize };
        _spectrum.SelectionChangedByUser += (_, _) => SetHsvFromUser(_spectrum.Hue, _spectrum.Saturation, _value);

        _preview = new BoxView { Color = Colors.White };
        _previousPreview = new BoxView { IsVisible = false };
        var previewGrid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star) } };
        previewGrid.Add(_preview);
        Grid.SetRowSpan(_preview, 2);
        previewGrid.Add(_previousPreview, 0, 1);
        _previewBorder = new Border
        {
            WidthRequest = PreviewWidth,
            Margin = new Thickness(Gap, 0, 0, 0),
            Padding = 0,
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromRgba(128, 128, 128, 96)),
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Content = previewGrid,
        };

        _slider = new ColorPickerSlider { HeightRequest = 20, Margin = new Thickness(0, Gap, 0, 0) };
        _slider.ValueChangedByUser += (_, _) => SetHsvFromUser(_hue, _saturation, _slider.Value);

        _representation = new ComboBox { WidthRequest = InputWidth, HeightRequest = InputHeight, FontSize = InputFontSize };
        _representation.Items.Add("RGB");
        _representation.Items.Add("HSV");
        _representation.SelectedIndex = 0;
        _representation.SelectionChanged += (_, _) => UpdateTextInputs();

        _hexBox = new Entry { WidthRequest = 132, HeightRequest = InputHeight, FontSize = InputFontSize, MaxLength = 7, HorizontalOptions = LayoutOptions.End, IsSpellCheckEnabled = false, IsTextPredictionEnabled = false };
        _hexBox.TextChanged += (_, e) => OnHexTextChanged(e.NewTextValue);
        _hexBox.Unfocused += (_, _) => UpdateTextInputs();
        _hexBox.Completed += (_, _) => UpdateTextInputs();

        _textEntryGrid = new Grid
        {
            Margin = new Thickness(0, Gap, 0, 0),
            ColumnDefinitions = { new ColumnDefinition(InputWidth), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        };
        _textEntryGrid.Add(_representation);
        _textEntryGrid.Add(_hexBox, 1, 0);
        for (var channel = 0; channel < 3; channel++)
        {
            var index = channel;
            var box = new Entry { WidthRequest = InputWidth, HeightRequest = InputHeight, FontSize = InputFontSize, MaxLength = 3, Keyboard = Keyboard.Numeric, Margin = new Thickness(0, Gap, 0, 0) };
            box.TextChanged += (_, e) => OnChannelTextChanged(index, e.NewTextValue);
            box.Unfocused += (_, _) => UpdateTextInputs();
            box.Completed += (_, _) => UpdateTextInputs();
            var label = new Label { FontSize = InputFontSize, Margin = new Thickness(8, Gap, 0, 0), VerticalOptions = LayoutOptions.Center };
            _channelBoxes[channel] = box;
            _channelLabels[channel] = label;
            _textEntryGrid.Add(box, 0, channel + 1);
            _textEntryGrid.Add(label, 1, channel + 1);
        }

        var root = new Grid
        {
            HorizontalOptions = LayoutOptions.Start,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        };
        root.Add(_spectrum);
        root.Add(_previewBorder, 1, 0);
        root.Add(_slider, 0, 1);
        Grid.SetColumnSpan(_slider, 2);
        root.Add(_textEntryGrid, 0, 2);
        Grid.SetColumnSpan(_textEntryGrid, 2);
        Content = root;

        ApplyVisibility();
        ApplyColorToHsv(Color);
        UpdateChildren();
    }

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(ColorPicker), Colors.White, BindingMode.TwoWay,
            propertyChanged: (bindable, oldValue, newValue) => ((ColorPicker)bindable).OnColorChanged((Color)oldValue, (Color)newValue));

    /// <summary>The chosen colour. Raises <see cref="ColorChanged"/> when it changes, from code or from the user.</summary>
    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty PreviousColorProperty =
        BindableProperty.Create(nameof(PreviousColor), typeof(Color), typeof(ColorPicker), null,
            propertyChanged: (bindable, _, _) => ((ColorPicker)bindable).UpdateChildren());

    /// <summary>When set, shown in the lower half of the preview strip, under the new colour.</summary>
    public Color? PreviousColor
    {
        get => (Color?)GetValue(PreviousColorProperty);
        set => SetValue(PreviousColorProperty, value);
    }

    /// <summary>
    /// Raised when <see cref="Color"/> changes. Typed as WinUI's
    /// TypedEventHandler&lt;ColorPicker, ColorChangedEventArgs&gt;, so a handler written for WinUI
    /// (ColorPicker sender, ColorChangedEventArgs args) attaches as it is.
    /// </summary>
    public event ColorChangedEventHandler? ColorChanged;

    public static readonly BindableProperty IsColorSpectrumVisibleProperty = VisibilityProperty(nameof(IsColorSpectrumVisible), true);
    public static readonly BindableProperty IsColorPreviewVisibleProperty = VisibilityProperty(nameof(IsColorPreviewVisible), true);
    public static readonly BindableProperty IsColorSliderVisibleProperty = VisibilityProperty(nameof(IsColorSliderVisible), true);
    public static readonly BindableProperty IsColorChannelTextInputVisibleProperty = VisibilityProperty(nameof(IsColorChannelTextInputVisible), true);
    public static readonly BindableProperty IsHexInputVisibleProperty = VisibilityProperty(nameof(IsHexInputVisible), true);
    public static readonly BindableProperty IsMoreButtonVisibleProperty = VisibilityProperty(nameof(IsMoreButtonVisible), false);
    public static readonly BindableProperty IsAlphaEnabledProperty = VisibilityProperty(nameof(IsAlphaEnabled), false);
    public static readonly BindableProperty IsAlphaSliderVisibleProperty = VisibilityProperty(nameof(IsAlphaSliderVisible), true);
    public static readonly BindableProperty IsAlphaTextInputVisibleProperty = VisibilityProperty(nameof(IsAlphaTextInputVisible), true);

    public bool IsColorSpectrumVisible { get => (bool)GetValue(IsColorSpectrumVisibleProperty); set => SetValue(IsColorSpectrumVisibleProperty, value); }

    public bool IsColorPreviewVisible { get => (bool)GetValue(IsColorPreviewVisibleProperty); set => SetValue(IsColorPreviewVisibleProperty, value); }

    public bool IsColorSliderVisible { get => (bool)GetValue(IsColorSliderVisibleProperty); set => SetValue(IsColorSliderVisibleProperty, value); }

    public bool IsColorChannelTextInputVisible { get => (bool)GetValue(IsColorChannelTextInputVisibleProperty); set => SetValue(IsColorChannelTextInputVisibleProperty, value); }

    public bool IsHexInputVisible { get => (bool)GetValue(IsHexInputVisibleProperty); set => SetValue(IsHexInputVisibleProperty, value); }

    /// <summary>Kept for converted XAML; the picker always shows its inputs (no "More" button).</summary>
    public bool IsMoreButtonVisible { get => (bool)GetValue(IsMoreButtonVisibleProperty); set => SetValue(IsMoreButtonVisibleProperty, value); }

    /// <summary>Kept for converted XAML; alpha is not edited (the colour stays opaque).</summary>
    public bool IsAlphaEnabled { get => (bool)GetValue(IsAlphaEnabledProperty); set => SetValue(IsAlphaEnabledProperty, value); }

    public bool IsAlphaSliderVisible { get => (bool)GetValue(IsAlphaSliderVisibleProperty); set => SetValue(IsAlphaSliderVisibleProperty, value); }

    public bool IsAlphaTextInputVisible { get => (bool)GetValue(IsAlphaTextInputVisibleProperty); set => SetValue(IsAlphaTextInputVisibleProperty, value); }

    public static readonly BindableProperty ColorSpectrumShapeProperty =
        BindableProperty.Create(nameof(ColorSpectrumShape), typeof(ColorSpectrumShape), typeof(ColorPicker), ColorSpectrumShape.Box);

    /// <summary>Kept for converted XAML; the spectrum is always the box.</summary>
    public ColorSpectrumShape ColorSpectrumShape { get => (ColorSpectrumShape)GetValue(ColorSpectrumShapeProperty); set => SetValue(ColorSpectrumShapeProperty, value); }

    public static readonly BindableProperty ColorSpectrumComponentsProperty =
        BindableProperty.Create(nameof(ColorSpectrumComponents), typeof(ColorSpectrumComponents), typeof(ColorPicker), ColorSpectrumComponents.HueSaturation);

    /// <summary>Kept for converted XAML; the spectrum is always hue by saturation.</summary>
    public ColorSpectrumComponents ColorSpectrumComponents { get => (ColorSpectrumComponents)GetValue(ColorSpectrumComponentsProperty); set => SetValue(ColorSpectrumComponentsProperty, value); }

    public static readonly BindableProperty OrientationProperty =
        BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(ColorPicker), StackOrientation.Vertical);

    /// <summary>Kept for converted XAML; the picker is always laid out vertically.</summary>
    public StackOrientation Orientation { get => (StackOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    private static BindableProperty VisibilityProperty(string name, bool defaultValue) =>
        BindableProperty.Create(name, typeof(bool), typeof(ColorPicker), defaultValue,
            propertyChanged: (bindable, _, _) => ((ColorPicker)bindable).ApplyVisibility());

    private void ApplyVisibility()
    {
        _spectrum.IsVisible = IsColorSpectrumVisible;
        _previewBorder.IsVisible = IsColorPreviewVisible;
        // Without the spectrum the preview stands alone: WinUI gives it the spectrum's place.
        _previewBorder.HeightRequest = IsColorSpectrumVisible ? -1 : PreviewWidth;
        _previewBorder.Margin = new Thickness(IsColorSpectrumVisible ? Gap : 0, 0, 0, 0);
        _slider.IsVisible = IsColorSliderVisible;
        _hexBox.IsVisible = IsHexInputVisible;
        _representation.IsVisible = IsColorChannelTextInputVisible;
        for (var channel = 0; channel < 3; channel++)
        {
            _channelBoxes[channel].IsVisible = IsColorChannelTextInputVisible;
            _channelLabels[channel].IsVisible = IsColorChannelTextInputVisible;
        }

        _textEntryGrid.IsVisible = IsHexInputVisible || IsColorChannelTextInputVisible;
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        if (!_updating)
        {
            ApplyColorToHsv(newColor);
            UpdateChildren();
        }

        ColorChanged?.Invoke(this, new ColorChangedEventArgs(oldColor, newColor));
    }

    // A colour set from outside: its hue, saturation and value, keeping what the colour cannot say.
    private void ApplyColorToHsv(Color color)
    {
        RgbToHsv(ToByte(color.Red), ToByte(color.Green), ToByte(color.Blue), out var hue, out var saturation, out var value);
        _value = value;
        if (value > 0)
        {
            _saturation = saturation;
            if (saturation > 0)
            {
                _hue = hue;
            }
        }
    }

    private void SetHsvFromUser(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = saturation;
        _value = value;
        PushHsvToColor();
    }

    private void PushHsvToColor()
    {
        HsvToRgb(_hue, _saturation, _value, out var red, out var green, out var blue);
        _updating = true;
        try
        {
            Color = Color.FromRgb(red, green, blue);
        }
        finally
        {
            _updating = false;
        }

        UpdateChildren();
    }

    private void UpdateChildren()
    {
        _spectrum.Hue = _hue;
        _spectrum.Saturation = _saturation;
        _spectrum.Value = _value;
        _slider.Hue = _hue;
        _slider.Saturation = _saturation;
        _slider.Value = _value;
        _preview.Color = Color;
        _previousPreview.IsVisible = PreviousColor is not null;
        Grid.SetRowSpan(_preview, PreviousColor is null ? 2 : 1);
        if (PreviousColor is { } previous)
        {
            _previousPreview.Color = previous;
        }

        UpdateTextInputs();
    }

    private bool ShowsHsv => _representation.SelectedIndex == 1;

    // Writes the boxes from the colour, leaving alone the one being typed in: rewriting it would
    // move the caret, and "0" would come back while the user clears the box to type.
    private void UpdateTextInputs()
    {
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            var names = ColorPickerStrings.For(CultureInfo.CurrentUICulture);
            var red = ToByte(Color.Red);
            var green = ToByte(Color.Green);
            var blue = ToByte(Color.Blue);
            var values = ShowsHsv
                ? new[] { (int)Math.Round(_hue) % 360, (int)Math.Round(_saturation * 100), (int)Math.Round(_value * 100) }
                : new[] { red, green, blue };
            for (var channel = 0; channel < 3; channel++)
            {
                _channelLabels[channel].Text = names[(ShowsHsv ? 3 : 0) + channel];
                if (!_channelBoxes[channel].IsFocused)
                {
                    _channelBoxes[channel].Text = values[channel].ToString(CultureInfo.InvariantCulture);
                }
            }

            if (!_hexBox.IsFocused)
            {
                _hexBox.Text = $"#{red:X2}{green:X2}{blue:X2}";
            }
        }
        finally
        {
            _updating = wasUpdating;
        }
    }

    // As in WinUI, a channel box applies what is typed as soon as it is a valid value.
    private void OnChannelTextChanged(int channel, string? text)
    {
        if (_updating || !_channelBoxes[channel].IsFocused ||
            !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var typed))
        {
            return;
        }

        if (ShowsHsv)
        {
            var hue = channel == 0 ? Math.Clamp(typed, 0, 359) : _hue;
            var saturation = channel == 1 ? Math.Clamp(typed, 0, 100) / 100d : _saturation;
            var value = channel == 2 ? Math.Clamp(typed, 0, 100) / 100d : _value;
            SetHsvFromUser(hue, saturation, value);
            return;
        }

        var red = channel == 0 ? Math.Clamp(typed, 0, 255) : ToByte(Color.Red);
        var green = channel == 1 ? Math.Clamp(typed, 0, 255) : ToByte(Color.Green);
        var blue = channel == 2 ? Math.Clamp(typed, 0, 255) : ToByte(Color.Blue);
        Color = Color.FromRgb(red, green, blue);
    }

    private void OnHexTextChanged(string? text)
    {
        if (_updating || !_hexBox.IsFocused)
        {
            return;
        }

        var hex = text?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            Color = Color.FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }

    private static int ToByte(float component) => Math.Clamp((int)Math.Round(component * 255d), 0, 255);

    internal static void HsvToRgb(double hue, double saturation, double value, out int red, out int green, out int blue)
    {
        hue = ((hue % 360) + 360) % 360;
        var chroma = value * saturation;
        var sector = hue / 60;
        var second = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, second, 0d),
            1 => (second, chroma, 0d),
            2 => (0d, chroma, second),
            3 => (0d, second, chroma),
            4 => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };
        var offset = value - chroma;
        red = Math.Clamp((int)Math.Round((r + offset) * 255), 0, 255);
        green = Math.Clamp((int)Math.Round((g + offset) * 255), 0, 255);
        blue = Math.Clamp((int)Math.Round((b + offset) * 255), 0, 255);
    }

    internal static void RgbToHsv(int red, int green, int blue, out double hue, out double saturation, out double value)
    {
        var r = red / 255d;
        var g = green / 255d;
        var b = blue / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        value = max;
        saturation = max <= 0 ? 0 : delta / max;
        if (delta <= 0)
        {
            hue = 0;
            return;
        }

        hue = max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        if (hue < 0)
        {
            hue += 360;
        }
    }
}

/// <summary>
/// The picker's channel names. WinUI brings its own translations; these are the same words in the
/// languages SmartCAD ships, English for any other.
/// </summary>
internal static class ColorPickerStrings
{
    // Red, Green, Blue, Hue, Saturation, Value.
    private static readonly string[] English = ["Red", "Green", "Blue", "Hue", "Saturation", "Value"];

    private static readonly Dictionary<string, string[]> ByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = ["Rot", "Grün", "Blau", "Farbton", "Sättigung", "Wert"],
        ["fr"] = ["Rouge", "Vert", "Bleu", "Teinte", "Saturation", "Valeur"],
        ["ru"] = ["Красный", "Зеленый", "Синий", "Оттенок", "Насыщенность", "Значение"],
        ["zh"] = ["红色", "绿色", "蓝色", "色调", "饱和度", "值"],
    };

    public static string[] For(CultureInfo culture) =>
        ByLanguage.TryGetValue(culture.TwoLetterISOLanguageName, out var names) ? names : English;
}
