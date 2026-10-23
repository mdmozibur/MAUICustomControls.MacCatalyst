namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ProgressRing = MAUICustomControls.MacCatalyst.Controls.ProgressRing;

/// <summary>
/// WinUI's ProgressRing drawn on Linux: a spinning arc when indeterminate, or an arc for the
/// fraction over a faint track. Inactive, it keeps its slot and draws nothing.
/// </summary>
public sealed class ProgressRingView : Avalonia.Controls.Control
{
    private const double DefaultSize = 32;
    private readonly DispatcherTimer _timer;
    private double _angle;

    public ProgressRingView()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            _angle = (_angle + 6) % 360;
            InvalidateVisual();
        });
    }

    public bool IsActive { get; set { field = value; UpdateTimer(); InvalidateVisual(); } } = true;

    public bool IsIndeterminate { get; set { field = value; UpdateTimer(); InvalidateVisual(); } } = true;

    public double Fraction { get; set { field = value; InvalidateVisual(); } }

    public Avalonia.Media.Color? ArcColor { get; set { field = value; InvalidateVisual(); } }

    public Avalonia.Media.Color? TrackColor { get; set { field = value; InvalidateVisual(); } }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(DefaultSize, availableSize.Width), Math.Min(DefaultSize, availableSize.Height));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (!IsActive)
        {
            return;
        }

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var thickness = Math.Max(2, size / 10);
        var radius = (size - thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var accent = ArcColor ?? LinuxControlSupport.AccentColor(this);

        if (!IsIndeterminate)
        {
            var track = TrackColor ?? LinuxControlSupport.ThemeForeground(this).WithAlpha(0.15);
            context.DrawEllipse(null, new Pen(new SolidColorBrush(track), thickness), center, radius, radius);
        }

        var start = IsIndeterminate ? _angle : 0;
        var sweep = IsIndeterminate ? 100 : Math.Clamp(Fraction, 0, 1) * 360;
        if (sweep <= 0)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(accent), thickness, lineCap: PenLineCap.Round);
        if (sweep >= 359.9)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        context.DrawGeometry(null, pen, Arc(center, radius, start - 90, sweep));
    }

    private static StreamGeometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        static Point On(Point c, double r, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            return new Point(c.X + r * Math.Cos(radians), c.Y + r * Math.Sin(radians));
        }

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(On(center, radius, startDegrees), false);
        ctx.ArcTo(On(center, radius, startDegrees + sweepDegrees), new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geometry;
    }

    private void UpdateTimer()
    {
        if (IsActive && IsIndeterminate && TopLevel.GetTopLevel(this) is not null)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }
}

public sealed class ProgressRingHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ProgressRing, ProgressRingView>
{
    public static readonly PropertyMapper<ProgressRing, ProgressRingHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ProgressRing.IsActive)] = (handler, ring) => handler.PlatformView.IsActive = ring.IsActive,
        [nameof(ProgressRing.IsIndeterminate)] = (handler, ring) => handler.PlatformView.IsIndeterminate = ring.IsIndeterminate,
        [nameof(ProgressRing.Value)] = MapFraction,
        [nameof(ProgressRing.Minimum)] = MapFraction,
        [nameof(ProgressRing.Maximum)] = MapFraction,
        [nameof(ProgressRing.TextColor)] = (handler, ring) => handler.PlatformView.ArcColor = ring.TextColor?.ToAvalonia(),
        [nameof(ProgressRing.TrackColor)] = (handler, ring) => handler.PlatformView.TrackColor = ring.TrackColor?.ToAvalonia(),
    };

    public ProgressRingHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    protected override ProgressRingView CreatePlatformView() => new();

    private static void MapFraction(ProgressRingHandler handler, ProgressRing ring) => handler.PlatformView.Fraction = ring.Fraction;
}
