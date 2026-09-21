using CoreGraphics;
using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class SegmentedButtonsHandler : ViewHandler<SegmentedButtons, UIStackView>
{
    private const int NoSelectedSegment = -1;
    private const double IconTextSpacing = 6;

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

        _segmentedControl = new KeyboardSegmentedControl();
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

    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (_segmentedControl is null || _headerLabel is null)
        {
            return base.GetDesiredSize(widthConstraint, heightConstraint);
        }

        // UIStackView sizes itself through Auto Layout, not SizeThatFits, which is what MAUI's default
        // measure asks - so the control measured as empty and was never shown. Measure the parts.
        var segments = _segmentedControl.IntrinsicContentSize;
        double width = segments.Width;
        double height = segments.Height;

        if (!_headerLabel.Hidden)
        {
            var headerWidthLimit = double.IsInfinity(widthConstraint) ? nfloat.MaxValue : (nfloat)widthConstraint;
            var header = _headerLabel.SizeThatFits(new CGSize(headerWidthLimit, nfloat.MaxValue));
            width = Math.Max(width, header.Width);
            height += header.Height + PlatformView.Spacing;
        }

        if (VirtualView.WidthRequest >= 0)
        {
            width = VirtualView.WidthRequest;
        }

        if (VirtualView.HeightRequest >= 0)
        {
            height = VirtualView.HeightRequest;
        }

        return new Size(Math.Ceiling(Math.Min(width, widthConstraint)), Math.Ceiling(Math.Min(height, heightConstraint)));
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
            var item = control.Items[index];
            var text = item.GetContentText();
            var glyph = item.GetIconGlyph(out var iconFontFamily);

            if (string.IsNullOrEmpty(glyph))
            {
                handler._segmentedControl.InsertSegment(text, index, false);
            }
            else
            {
                handler._segmentedControl.InsertSegment(
                    CreateIconSegmentImage(glyph, iconFontFamily, text, (nfloat)control.FontSize), index, false);
            }
        }

        MapSelectedIndex(handler, control);

        // Item text often arrives after the first measure (x:Uid localization sets it later).
        ((IView)control).InvalidateMeasure();
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
        handler._headerLabel.TextColor = ResolveTextColor(control);
        handler._headerLabel.Hidden = string.IsNullOrWhiteSpace(header);

        // VoiceOver reads the header as the segments' group name; the caption itself is not read twice.
        handler._headerLabel.IsAccessibilityElement = false;
        if (handler._segmentedControl is not null)
        {
            handler._segmentedControl.AccessibilityLabel = string.IsNullOrWhiteSpace(header)
                ? Microsoft.Maui.Controls.SemanticProperties.GetDescription(control)
                : header;
        }

        ((IView)control).InvalidateMeasure();
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
                ForegroundColor = ResolveTextColor(control),
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

        // Icon segments are drawn at the font size.
        MapItems(handler, control);
    }

    // UIColor.Label, not a fixed colour: it is dynamic, so the header and unselected titles follow
    // the light/dark theme. A fixed default (it used to be black) left them unreadable in dark mode.
    private static UIColor ResolveTextColor(SegmentedButtons control)
    {
        return control.TextColor?.ToPlatform() ?? UIColor.Label;
    }

    private static void MapTintColor(SegmentedButtonsHandler handler, SegmentedButtons control)
    {
        if (handler._segmentedControl is not null)
        {
            handler._segmentedControl.SelectedSegmentTintColor = control.TintColor.ToPlatform();
        }
    }

    /// <summary>
    /// Draws an item's icon glyph and label into one template image.
    /// </summary>
    /// <remarks>
    /// UWP's SegmentedButtonItem shows the icon next to the text. A UISegmentedControl segment holds
    /// either a title or an image, and the glyph lives in the app's icon font, which a title cannot mix
    /// with the system font.
    /// </remarks>
    private static UIImage CreateIconSegmentImage(string glyph, string? iconFontFamily, string text, nfloat fontSize)
    {
        var iconFont = (string.IsNullOrEmpty(iconFontFamily) ? null : ToggleDropdownHandler.ResolvePlatformFont(iconFontFamily, fontSize))
            ?? UIFont.SystemFontOfSize(fontSize);
        var icon = new NSAttributedString(glyph, new UIStringAttributes { Font = iconFont, ForegroundColor = UIColor.Black });
        var label = new NSAttributedString(text, new UIStringAttributes { Font = UIFont.SystemFontOfSize(fontSize), ForegroundColor = UIColor.Black });

        var hasText = text.Length > 0;
        var iconSize = icon.Size;
        var labelSize = hasText ? label.Size : CGSize.Empty;
        var spacing = hasText ? IconTextSpacing : 0;
        var imageSize = new CGSize(
            Math.Ceiling(iconSize.Width + spacing + labelSize.Width),
            Math.Ceiling(Math.Max(iconSize.Height, labelSize.Height)));

        var image = new UIGraphicsImageRenderer(imageSize).CreateImage(_ =>
        {
            icon.DrawString(new CGPoint(0, (imageSize.Height - iconSize.Height) / 2));

            if (hasText)
            {
                label.DrawString(new CGPoint(iconSize.Width + spacing, (imageSize.Height - labelSize.Height) / 2));
            }
        });

        // A template image is tinted by the control for its normal and selected states.
        var segmentImage = image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
        segmentImage.AccessibilityLabel = hasText ? text : glyph;
        return segmentImage;
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

/// <summary>
/// The segments as UWP's SegmentedButtons answer the keyboard: with keyboard focus (keyboard
/// navigation on), ← and → select the previous or next segment, as a click would.
/// </summary>
internal sealed class KeyboardSegmentedControl : UISegmentedControl
{
    private readonly HashSet<UIKeyboardHidUsage> _swallowed = new();

    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (Focused && Enabled && KeyboardKeys.KeyOf(presses) is { } key
            && key is UIKeyboardHidUsage.KeyboardLeftArrow or UIKeyboardHidUsage.KeyboardRightArrow)
        {
            _swallowed.Add(key);
            var next = (int)SelectedSegment + (key == UIKeyboardHidUsage.KeyboardLeftArrow ? -1 : 1);
            if (next >= 0 && next < NumberOfSegments && next != SelectedSegment)
            {
                SelectedSegment = next;
                SendActionForControlEvents(UIControlEvent.ValueChanged);
            }

            return;
        }

        base.PressesBegan(presses, evt);
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (KeyboardKeys.KeyOf(presses, withoutModifiers: false) is { } key && _swallowed.Remove(key))
        {
            return;
        }

        base.PressesEnded(presses, evt);
    }
}
