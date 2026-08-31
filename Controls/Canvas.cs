using Microsoft.Maui.Layouts;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A UWP-compatible coordinate layout. Children are measured without constraints,
/// do not contribute to the Canvas desired size, and are arranged from Canvas.Left
/// and Canvas.Top. Canvas.ZIndex is mirrored to MAUI's native z-order property.
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

    protected override ILayoutManager CreateLayoutManager() => new CanvasLayoutManager(this);

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
