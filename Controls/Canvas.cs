using Microsoft.Maui.Layouts;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A UWP-compatible coordinate layout. Children are measured without constraints,
/// do not contribute to the Canvas desired size, and are arranged from Canvas.Left
/// and Canvas.Top. Canvas.ZIndex is mirrored to MAUI's native z-order property.
/// A Canvas with no Background is transparent to pointer input, as in UWP.
/// </summary>
[ContentProperty(nameof(Children))]
public sealed class Canvas : Layout
{
    public static readonly BindableProperty LeftProperty = BindableProperty.CreateAttached(
        "Left",
        typeof(double),
        typeof(Canvas),
        double.NaN,
        propertyChanged: OnPositionChanged);

    public static readonly BindableProperty TopProperty = BindableProperty.CreateAttached(
        "Top",
        typeof(double),
        typeof(Canvas),
        double.NaN,
        propertyChanged: OnPositionChanged);

    public new static readonly BindableProperty ZIndexProperty = BindableProperty.CreateAttached(
        "ZIndex",
        typeof(int),
        typeof(Canvas),
        -1,
        propertyChanged: OnZIndexChanged);

    public static double GetLeft(BindableObject element) => (double)element.GetValue(LeftProperty);

    public static void SetLeft(BindableObject element, double value) => element.SetValue(LeftProperty, value);

    public static double GetTop(BindableObject element) => (double)element.GetValue(TopProperty);

    public static void SetTop(BindableObject element, double value) => element.SetValue(TopProperty, value);

    public static int GetZIndex(BindableObject element) => (int)element.GetValue(ZIndexProperty);

    public static void SetZIndex(BindableObject element, int value) => element.SetValue(ZIndexProperty, value);

    public Canvas()
    {
        // Only the layout itself opts out of hit testing; children keep their own input behaviour.
        CascadeInputTransparent = false;
        UpdateInputTransparency();
    }

    protected override ILayoutManager CreateLayoutManager() => new CanvasLayoutManager(this);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName is nameof(Background) or nameof(BackgroundColor))
        {
            UpdateInputTransparency();
        }
    }

    /// <summary>
    /// UWP hit-tests a Panel only where it actually paints: a Canvas without a Background lets
    /// pointer input fall through to whatever sits behind it, while its children still receive
    /// input. UIKit hit-tests a layout's view regardless of its background, so an unpainted Canvas
    /// laid over a sibling (for example the drawing surface) would swallow every pointer, pinch and
    /// scroll gesture. Mark the layout itself input transparent whenever it paints nothing;
    /// CascadeInputTransparent is false, so children are unaffected.
    /// </summary>
    private void UpdateInputTransparency()
    {
        InputTransparent = PaintsNothing(Background, BackgroundColor);
    }

    private static bool PaintsNothing(Brush? background, Color? backgroundColor)
    {
        return Brush.IsNullOrEmpty(background) &&
               (backgroundColor is null || backgroundColor.Alpha <= 0f);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);

        if (child is VisualElement visualElement)
        {
            visualElement.ZIndex = GetZIndex(visualElement);
        }
    }

    private static void OnPositionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Element { Parent: Canvas canvas })
        {
            canvas.InvalidateMeasure();
        }
    }

    private static void OnZIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement visualElement)
        {
            visualElement.ZIndex = (int)newValue;
        }

        OnPositionChanged(bindable, oldValue, newValue);
    }

    private sealed class CanvasLayoutManager(Canvas canvas) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            foreach (var child in canvas)
            {
                if (child.Visibility != Visibility.Collapsed)
                {
                    child.Measure(double.PositiveInfinity, double.PositiveInfinity);
                }
            }

            // This intentionally ignores every child's desired size. As in UWP,
            // an Auto-sized Canvas with no explicit dimensions has no desired size.
            return Size.Zero;
        }

        public Size ArrangeChildren(Rect bounds)
        {
            foreach (var child in canvas)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                var bindableChild = (BindableObject)child;
                var left = GetLeft(bindableChild);
                var top = GetTop(bindableChild);
                var desired = child.DesiredSize;

                child.Arrange(new Rect(
                    bounds.X + (double.IsNaN(left) ? 0 : left),
                    bounds.Y + (double.IsNaN(top) ? 0 : top),
                    desired.Width,
                    desired.Height));
            }

            return bounds.Size;
        }
    }
}
