using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class SegmentedButtonsHandler : ViewHandler<SegmentedButtons, UIStackView>
{
    private const int NoSelectedSegment = -1;

    private UILabel? _headerLabel;
    private UISegmentedControl? _segmentedControl;

    public static readonly IPropertyMapper<SegmentedButtons, SegmentedButtonsHandler> Mapper =
        new PropertyMapper<SegmentedButtons, SegmentedButtonsHandler>(ViewMapper)
        {
            [nameof(SegmentedButtons.Items)] = MapItems,
            [nameof(SegmentedButtons.Header)] = MapHeader,
            [nameof(SegmentedButtons.SelectedIndex)] = MapSelectedIndex,
            [nameof(SegmentedButtons.FontSize)] = MapTextStyle,
            [nameof(SegmentedButtons.TextColor)] = MapTextStyle,
            [nameof(SegmentedButtons.SelectedTextColor)] = MapTextStyle,
            [nameof(SegmentedButtons.TintColor)] = MapTintColor,
        };

    public SegmentedButtonsHandler()
        : base(Mapper)
    {
    }

    protected override UIStackView CreatePlatformView()
    {
        _headerLabel = new UILabel
        {
            Lines = 0,
            TextAlignment = UITextAlignment.Left,
        };

        _segmentedControl = new UISegmentedControl();
        _segmentedControl.ValueChanged += OnValueChanged;

        return new UIStackView([_headerLabel, _segmentedControl])
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 4,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.Fill,
        };
    }

    protected override void ConnectHandler(UIStackView platformView)
    {
        base.ConnectHandler(platformView);
        MapItems(this, VirtualView);
        MapHeader(this, VirtualView);
        MapTextStyle(this, VirtualView);
        MapTintColor(this, VirtualView);
        MapSelectedIndex(this, VirtualView);
    }

    protected override void DisconnectHandler(UIStackView platformView)
    {
        if (_segmentedControl is not null)
        {
            _segmentedControl.ValueChanged -= OnValueChanged;
        }

        base.DisconnectHandler(platformView);
    }

    private static void MapItems(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._segmentedControl is null)
        {
            return;
        }

        handler._segmentedControl.RemoveAllSegments();
        for (var index = 0; index < control.Items.Count; index++)
        {
            handler._segmentedControl.InsertSegment(control.Items[index].GetDisplayText(), index, false);
        }

        MapSelectedIndex(handler, control);
    }

    private static void MapHeader(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._headerLabel is null)
        {
            return;
        }

        var header = control.GetHeaderText();
        handler._headerLabel.Text = header;
        handler._headerLabel.Font = UIFont.SystemFontOfSize((nfloat)control.FontSize, UIFontWeight.Semibold)!;
        handler._headerLabel.TextColor = control.TextColor.ToPlatform();
        handler._headerLabel.Hidden = string.IsNullOrWhiteSpace(header);
    }

    private static void MapSelectedIndex(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._segmentedControl is not null)
        {
            handler._segmentedControl.SelectedSegment = control.SelectedIndex >= 0
                ? control.SelectedIndex
                : NoSelectedSegment;
        }
    }

    private static void MapTextStyle(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._segmentedControl is null)
        {
            return;
        }

        var font = UIFont.SystemFontOfSize((nfloat)control.FontSize);
        handler._segmentedControl.SetTitleTextAttributes(
            new UIStringAttributes
            {
                Font = font,
                ForegroundColor = control.TextColor.ToPlatform(),
            },
            UIControlState.Normal);
        handler._segmentedControl.SetTitleTextAttributes(
            new UIStringAttributes
            {
                Font = font,
                ForegroundColor = control.SelectedTextColor.ToPlatform(),
            },
            UIControlState.Selected);

        MapHeader(handler, control);
    }

    private static void MapTintColor(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._segmentedControl is not null)
        {
            handler._segmentedControl.SelectedSegmentTintColor = control.TintColor.ToPlatform();
        }
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        if (VirtualView is null || _segmentedControl is null)
        {
            return;
        }

        VirtualView.SelectedIndex = _segmentedControl.SelectedSegment == NoSelectedSegment
            ? -1
            : (int)_segmentedControl.SelectedSegment;
    }
}
