using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// Base type for icon sources accepted by <see cref="InfoBadge"/>.
/// </summary>
public abstract class InfoBadgeIconSource : BindableObject
{
}

/// <summary>
/// The WinUI FontIconSource subset used by converted projects. Changes are
/// bindable and are reflected immediately by an InfoBadge that owns the source.
/// </summary>
public sealed class FontIconSource : InfoBadgeIconSource
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(FontIconSource), string.Empty);

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(FontIconSource), null);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(FontIconSource), 11d);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(FontIconSource), null);

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
}

/// <summary>
/// A WinUI 2 InfoBadge replacement with dot, icon, and numeric modes. Value has
/// precedence over IconSource, and the badge remains circular until its content
/// requires a wider pill, matching the WinUI control contract.
/// </summary>
public sealed class InfoBadge : ContentView
{
    private static readonly CornerRadius AutomaticCornerRadius = new(double.NaN);

    private readonly Border _surface;
    private readonly Grid _contentGrid;
    private readonly Label _valueLabel;
    private readonly Label _iconLabel;
    private readonly Image _iconImage;
    private INotifyPropertyChanged? _observedIconSource;

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(int),
        typeof(InfoBadge),
        -1,
        validateValue: static (_, value) => (int)value >= -1,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(
        nameof(IconSource),
        typeof(object),
        typeof(InfoBadge),
        null,
        propertyChanged: OnIconSourceChanged);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(InfoBadge),
        Colors.White,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(InfoBadge),
        AutomaticCornerRadius,
        propertyChanged: OnAppearanceChanged);

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "InfoBadge.Value must be -1 or a non-negative integer.");
            }

            SetValue(ValueProperty, value);
        }
    }

    /// <summary>
    /// Accepts a converted FontIconSource, a MAUI ImageSource, or null.
    /// </summary>
    public object? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public InfoBadge()
    {
        InputTransparent = true;
        Background = new SolidColorBrush(Color.FromArgb("#0078D4"));

        _valueLabel = new Label
        {
            FontSize = 11,
            Padding = new Thickness(4, 0, 4, 2),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            IsVisible = false,
        };

        _iconLabel = new Label
        {
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            IsVisible = false,
        };

        _iconImage = new Image
        {
            WidthRequest = 9,
            HeightRequest = 9,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };

        _contentGrid = new Grid
        {
            Children = { _valueLabel, _iconLabel, _iconImage },
        };

        _surface = new Border
        {
            Padding = 0,
            StrokeThickness = 0,
            Content = _contentGrid,
        };
        _surface.SetBinding(BackgroundProperty, new Binding(nameof(Background), source: this));

        base.Content = _surface;
        SizeChanged += (_, _) => UpdateCornerRadius();
        UpdateAppearance();
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var desired = base.MeasureOverride(widthConstraint, heightConstraint);
        return new Size(Math.Max(desired.Width, desired.Height), desired.Height);
    }

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((InfoBadge)bindable).UpdateAppearance();

    private static void OnIconSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var badge = (InfoBadge)bindable;
        badge.ObserveIconSource(oldValue as INotifyPropertyChanged, newValue as INotifyPropertyChanged);
        badge.UpdateAppearance();
    }

    private void ObserveIconSource(INotifyPropertyChanged? oldSource, INotifyPropertyChanged? newSource)
    {
        if (oldSource != null)
        {
            oldSource.PropertyChanged -= OnIconSourcePropertyChanged;
        }

        _observedIconSource = newSource;
        if (_observedIconSource != null)
        {
            _observedIconSource.PropertyChanged += OnIconSourcePropertyChanged;
        }
    }

    private void OnIconSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateAppearance();

    private void UpdateAppearance()
    {
        if (_surface == null)
        {
            return;
        }

        _valueLabel.TextColor = TextColor;
        _iconLabel.TextColor = TextColor;
        _valueLabel.IsVisible = false;
        _iconLabel.IsVisible = false;
        _iconImage.IsVisible = false;

        var hasValue = Value >= 0;
        var hasIcon = !hasValue && IconSource != null;

        if (hasValue)
        {
            _valueLabel.Text = Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            _valueLabel.IsVisible = true;
        }
        else if (IconSource is FontIconSource fontIconSource)
        {
            _iconLabel.Text = fontIconSource.Glyph;
            _iconLabel.FontFamily = fontIconSource.FontFamily;
            _iconLabel.FontSize = fontIconSource.FontSize;
            _iconLabel.TextColor = fontIconSource.TextColor ?? TextColor;
            _iconLabel.IsVisible = true;
        }
        else if (IconSource is ImageSource imageSource)
        {
            _iconImage.Source = imageSource;
            _iconImage.IsVisible = true;
        }

        if (hasValue || hasIcon)
        {
            _surface.WidthRequest = -1;
            _surface.HeightRequest = 16;
            _surface.MinimumWidthRequest = 16;
            _surface.MinimumHeightRequest = 16;
        }
        else
        {
            _surface.WidthRequest = 4;
            _surface.HeightRequest = 4;
            _surface.MinimumWidthRequest = 4;
            _surface.MinimumHeightRequest = 4;
        }

        var semanticText = hasValue
            ? _valueLabel.Text
            : IconSource is FontIconSource source
                ? source.Glyph
                : null;
        SemanticProperties.SetDescription(this, semanticText);

        UpdateCornerRadius();
        InvalidateMeasure();
    }

    private void UpdateCornerRadius()
    {
        if (_surface == null)
        {
            return;
        }

        var radius = double.IsNaN(CornerRadius.TopLeft)
            ? Math.Max(0, (Height > 0 ? Height : _surface.HeightRequest) / 2)
            : CornerRadius.TopLeft;
        _surface.StrokeShape = new RoundRectangle
        {
            CornerRadius = double.IsNaN(CornerRadius.TopLeft)
                ? new CornerRadius(radius)
                : CornerRadius,
        };
    }
}
