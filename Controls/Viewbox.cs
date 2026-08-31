using Microsoft.Maui.Layouts;

namespace MAUICustomControls.MacCatalyst.Controls;

public enum ViewboxStretch
{
    None,
    Fill,
    Uniform,
    UniformToFill,
}

public enum ViewboxStretchDirection
{
    UpOnly,
    DownOnly,
    Both,
}

/// <summary>
/// A single-child UWP Viewbox replacement. The child is measured unconstrained
/// and a presentation wrapper is scaled during arrange so the child's own MAUI
/// transforms remain intact.
/// </summary>
[ContentProperty(nameof(Child))]
public sealed class Viewbox : Layout
{
    private readonly ContentView _presenter;

    public static readonly BindableProperty ChildProperty = BindableProperty.Create(
        nameof(Child),
        typeof(View),
        typeof(Viewbox),
        null,
        propertyChanged: OnChildChanged);

    public static readonly BindableProperty StretchProperty = BindableProperty.Create(
        nameof(Stretch),
        typeof(ViewboxStretch),
        typeof(Viewbox),
        ViewboxStretch.Uniform,
        propertyChanged: OnLayoutPropertyChanged);

    public static readonly BindableProperty StretchDirectionProperty = BindableProperty.Create(
        nameof(StretchDirection),
        typeof(ViewboxStretchDirection),
        typeof(Viewbox),
        ViewboxStretchDirection.Both,
        propertyChanged: OnLayoutPropertyChanged);

    public View? Child
    {
        get => (View?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public ViewboxStretch Stretch
    {
        get => (ViewboxStretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public ViewboxStretchDirection StretchDirection
    {
        get => (ViewboxStretchDirection)GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public Viewbox()
    {
        _presenter = new ContentView
        {
            AnchorX = 0,
            AnchorY = 0,
        };

        Children.Add(_presenter);
    }

    protected override ILayoutManager CreateLayoutManager() => new ViewboxLayoutManager(this);

    /// <summary>
    /// Implements the UWP/WPF Viewbox scale contract, including a single finite
    /// axis and the UpOnly/DownOnly clamps.
    /// </summary>
    public static Size ComputeScaleFactor(
        Size availableSize,
        Size contentSize,
        ViewboxStretch stretch,
        ViewboxStretchDirection stretchDirection)
    {
        var widthConstrained = !double.IsPositiveInfinity(availableSize.Width);
        var heightConstrained = !double.IsPositiveInfinity(availableSize.Height);

        var scaleX = contentSize.Width > 0 ? availableSize.Width / contentSize.Width : 0;
        var scaleY = contentSize.Height > 0 ? availableSize.Height / contentSize.Height : 0;

        if (!widthConstrained)
        {
            scaleX = 1;
        }

        if (!heightConstrained)
        {
            scaleY = 1;
        }

        if (stretch == ViewboxStretch.None || (!widthConstrained && !heightConstrained))
        {
            scaleX = scaleY = 1;
        }
        else if (stretch is ViewboxStretch.Uniform or ViewboxStretch.UniformToFill)
        {
            var scale = !widthConstrained
                ? scaleY
                : !heightConstrained
                    ? scaleX
                    : stretch == ViewboxStretch.Uniform
                        ? Math.Min(scaleX, scaleY)
                        : Math.Max(scaleX, scaleY);

            scaleX = scaleY = scale;
        }

        switch (stretchDirection)
        {
            case ViewboxStretchDirection.UpOnly:
                scaleX = Math.Max(1, scaleX);
                scaleY = Math.Max(1, scaleY);
                break;
            case ViewboxStretchDirection.DownOnly:
                scaleX = Math.Min(1, scaleX);
                scaleY = Math.Min(1, scaleY);
                break;
        }

        return new Size(SanitizeScale(scaleX), SanitizeScale(scaleY));
    }

    private static double SanitizeScale(double scale) =>
        double.IsNaN(scale) || double.IsInfinity(scale) ? 0 : Math.Max(0, scale);

    private static void OnChildChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewbox = (Viewbox)bindable;
        viewbox._presenter.Content = (View?)newValue;
        viewbox.InvalidateMeasure();
    }

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewbox = (Viewbox)bindable;
        viewbox.IsClippedToBounds = viewbox.Stretch == ViewboxStretch.UniformToFill;
        viewbox.InvalidateMeasure();
    }

    private sealed class ViewboxLayoutManager(Viewbox viewbox) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            if (viewbox.Child == null)
            {
                return Size.Zero;
            }

            var contentSize = viewbox._presenter.Measure(
                double.PositiveInfinity,
                double.PositiveInfinity);
            var scale = ComputeScaleFactor(
                new Size(widthConstraint, heightConstraint),
                contentSize,
                viewbox.Stretch,
                viewbox.StretchDirection);

            return new Size(contentSize.Width * scale.Width, contentSize.Height * scale.Height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            if (viewbox.Child == null)
            {
                viewbox._presenter.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
                return bounds.Size;
            }

            var contentSize = viewbox._presenter.DesiredSize;
            var scale = ComputeScaleFactor(
                bounds.Size,
                contentSize,
                viewbox.Stretch,
                viewbox.StretchDirection);
            var renderedWidth = contentSize.Width * scale.Width;
            var renderedHeight = contentSize.Height * scale.Height;

            viewbox._presenter.ScaleX = scale.Width;
            viewbox._presenter.ScaleY = scale.Height;
            viewbox._presenter.Arrange(new Rect(
                bounds.X + ((bounds.Width - renderedWidth) / 2),
                bounds.Y + ((bounds.Height - renderedHeight) / 2),
                contentSize.Width,
                contentSize.Height));

            return bounds.Size;
        }
    }
}
