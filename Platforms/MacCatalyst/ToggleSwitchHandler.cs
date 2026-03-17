using CoreGraphics;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleSwitchHandler : ViewHandler<ToggleSwitch, ToggleSwitchHandler.ToggleSwitchView>
{
    public static PropertyMapper<ToggleSwitch, ToggleSwitchHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ToggleSwitch.Text)] = MapText,
        [nameof(ToggleSwitch.IsOn)] = MapIsOn,
        [nameof(ToggleSwitch.Foreground)] = MapForeground,
        [nameof(ToggleSwitch.OnColor)] = MapOnColor,
    };

    public ToggleSwitchHandler() : base(PropertyMapper)
    {
    }

    protected override ToggleSwitchView CreatePlatformView()
    {
        return new ToggleSwitchView();
    }

    protected override void ConnectHandler(ToggleSwitchView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.SwitchControl.AddTarget(ValueChanged, UIControlEvent.ValueChanged);

        MapText(this, VirtualView);
        MapIsOn(this, VirtualView);
        MapForeground(this, VirtualView);
        MapOnColor(this, VirtualView);
    }

    protected override void DisconnectHandler(ToggleSwitchView platformView)
    {
        platformView.SwitchControl.RemoveTarget(ValueChanged, UIControlEvent.ValueChanged);
        base.DisconnectHandler(platformView);
    }

    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        var width = double.IsInfinity(widthConstraint) ? ToggleSwitchView.MeasurementLimit : widthConstraint;
        var height = double.IsInfinity(heightConstraint) ? ToggleSwitchView.MeasurementLimit : heightConstraint;
        var fittingSize = PlatformView.SizeThatFits(new CGSize((nfloat)width, (nfloat)height));

        return new Size(fittingSize.Width, fittingSize.Height);
    }

    private void ValueChanged(object? sender, EventArgs e)
    {
        if (VirtualView.IsOn != PlatformView.SwitchControl.On)
        {
            VirtualView.IsOn = PlatformView.SwitchControl.On;
        }
    }

    public static void MapText(ToggleSwitchHandler handler, ToggleSwitch view)
    {
        handler.PlatformView.TextLabel.Text = view.Text;
        handler.PlatformView.InvalidateIntrinsicContentSize();
        handler.PlatformView.SetNeedsLayout();
    }

    public static void MapIsOn(ToggleSwitchHandler handler, ToggleSwitch view)
    {
        if (handler.PlatformView.SwitchControl.On != view.IsOn)
        {
            handler.PlatformView.SwitchControl.SetState(view.IsOn, true);
        }
    }

    public static void MapForeground(ToggleSwitchHandler handler, ToggleSwitch view)
    {
        handler.PlatformView.TextLabel.TextColor = view.Foreground.Color.ToPlatform();
    }

    public static void MapOnColor(ToggleSwitchHandler handler, ToggleSwitch view)
    {
        handler.PlatformView.SwitchControl.OnTintColor = view.OnColor.ToPlatform();
    }

    public sealed class ToggleSwitchView : UIView
    {
        private static readonly nfloat Spacing = 12;
        internal const double MeasurementLimit = 10000;

        public UILabel TextLabel { get; } = new()
        {
            Lines = 1,
            LineBreakMode = UILineBreakMode.TailTruncation,
        };

        public UISwitch SwitchControl { get; } = new();

        public ToggleSwitchView()
        {
            AddSubview(TextLabel);
            AddSubview(SwitchControl);
        }

        public override CGSize IntrinsicContentSize
        {
            get => SizeThatFits(new CGSize((nfloat)MeasurementLimit, (nfloat)MeasurementLimit));
        }

        public override CGSize SizeThatFits(CGSize size)
        {
            var switchSize = SwitchControl.SizeThatFits(new CGSize((nfloat)MeasurementLimit, (nfloat)MeasurementLimit));
            var spacing = GetSpacing();
            var unconstrainedLabelSize = MeasureLabel((nfloat)MeasurementLimit);

            var availableWidth = size.Width > 0 && size.Width < MeasurementLimit
                ? size.Width
                : unconstrainedLabelSize.Width + spacing + switchSize.Width;

            var labelMaxWidth = (nfloat)Math.Max(0, availableWidth - spacing - switchSize.Width);
            var labelSize = MeasureLabel(labelMaxWidth <= 0 ? unconstrainedLabelSize.Width : labelMaxWidth);
            var measuredWidth = labelSize.Width + spacing + switchSize.Width;

            if (size.Width > 0 && size.Width < MeasurementLimit)
            {
                measuredWidth = (nfloat)Math.Min(measuredWidth, size.Width);
            }

            var measuredHeight = (nfloat)Math.Max(labelSize.Height, switchSize.Height);
            return new CGSize(measuredWidth, measuredHeight);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var layoutSize = Bounds.Size;
            if (layoutSize.Width <= 0 || layoutSize.Height <= 0)
            {
                layoutSize = SizeThatFits(new CGSize((nfloat)MeasurementLimit, (nfloat)MeasurementLimit));
            }

            var switchSize = SwitchControl.SizeThatFits(layoutSize);
            var spacing = GetSpacing();
            var switchX = (nfloat)Math.Max(0, layoutSize.Width - switchSize.Width);
            var labelMaxWidth = (nfloat)Math.Max(0, switchX - spacing);
            var labelSize = MeasureLabel(labelMaxWidth);

            TextLabel.Frame = new CGRect(
                0,
                (layoutSize.Height - labelSize.Height) / 2,
                labelMaxWidth,
                labelSize.Height);

            SwitchControl.Frame = new CGRect(
                switchX,
                (layoutSize.Height - switchSize.Height) / 2,
                switchSize.Width,
                switchSize.Height);
        }

        private CGSize MeasureLabel(nfloat width)
        {
            var constrainedWidth = width > 0 ? width : (nfloat)MeasurementLimit;
            return TextLabel.SizeThatFits(new CGSize(constrainedWidth, (nfloat)MeasurementLimit));
        }

        private nfloat GetSpacing()
        {
            return string.IsNullOrWhiteSpace(TextLabel.Text) ? 0 : Spacing;
        }
    }
}