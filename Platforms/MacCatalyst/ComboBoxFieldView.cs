using CoreGraphics;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The collapsed field: a caller-supplied content view plus a chevron.
/// </summary>
/// <remarks>
/// A <see cref="UIControl"/> rather than a <see cref="UIButton"/> — UIButton's own
/// content handling fights arbitrary subviews, and the templated label is arbitrary
/// by definition.
/// </remarks>
public sealed class ComboBoxFieldView : UIControl
{
    private const double ChevronWidth = 14;
    private const double ChevronSpacing = 8;
    private const double MinimumContentHeight = 22;

    private readonly UIImageView _chevron;
    private UIView? _content;
    private bool _isOpen;
    private UIEdgeInsets _contentInsets = new(8, 12, 8, 12);

    public ComboBoxFieldView()
    {
        Layer.CornerRadius = 8;
        Layer.BorderWidth = 1;
        Layer.BorderColor = UIColor.Separator.CGColor;
        BackgroundColor = UIColor.SecondarySystemBackground;

        _chevron = new UIImageView(UIImage.GetSystemImage("chevron.down"))
        {
            ContentMode = UIViewContentMode.ScaleAspectFit,
            TintColor = UIColor.SecondaryLabel
        };
        AddSubview(_chevron);

        // Catalyst runs with a pointer, so the field gets a hover state.
        AddGestureRecognizer(new UIHoverGestureRecognizer(OnHover));
    }

    /// <summary>Space between the field's edges and its content, in points.</summary>
    public UIEdgeInsets ContentInsets
    {
        get => _contentInsets;
        set
        {
            _contentInsets = value;
            InvalidateIntrinsicContentSize();
            SetNeedsLayout();
        }
    }

    /// <summary>Radius of the field's rounded corners, in points.</summary>
    public double CornerRadius
    {
        get => Layer.CornerRadius;
        set => Layer.CornerRadius = (nfloat)value;
    }

    /// <summary>Width of the field's border, in points.</summary>
    public double BorderThickness
    {
        get => Layer.BorderWidth;
        set => Layer.BorderWidth = (nfloat)value;
    }

    /// <summary>Reflects the open state, so the field can show focus and flip the chevron.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            _isOpen = value;
            Layer.BorderColor = value ? UIColor.SystemBlue.CGColor : UIColor.Separator.CGColor;
            UIView.Animate(0.18, () =>
                _chevron.Transform = value
                    ? CGAffineTransform.MakeRotation((nfloat)Math.PI)
                    : CGAffineTransform.MakeIdentity());
        }
    }

    /// <summary>Swaps in the view for the current selection (or the placeholder).</summary>
    public void SetContent(UIView? content)
    {
        _content?.RemoveFromSuperview();
        _content = content;

        if (content is not null)
            AddSubview(content);

        InvalidateIntrinsicContentSize();
        SetNeedsLayout();
    }

    public override CGSize SizeThatFits(CGSize size)
    {
        var horizontalInset = (double)(_contentInsets.Left + _contentInsets.Right);
        var verticalInset = (double)(_contentInsets.Top + _contentInsets.Bottom);

        var available = size.Width > 0 && !double.IsInfinity(size.Width)
            ? size.Width - horizontalInset - ChevronWidth - ChevronSpacing
            : double.PositiveInfinity;

        var contentSize = _content?.SizeThatFits(new CGSize(available, double.PositiveInfinity)) ?? CGSize.Empty;
        var height = Math.Max(contentSize.Height, MinimumContentHeight) + verticalInset;
        var width = contentSize.Width + horizontalInset + ChevronWidth + ChevronSpacing;

        return new CGSize(size.Width > 0 ? size.Width : width, height);
    }

    public override CGSize IntrinsicContentSize => SizeThatFits(new CGSize(NoIntrinsicMetric, NoIntrinsicMetric));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var height = (double)Bounds.Height;
        var width = (double)Bounds.Width;

        _chevron.Frame = new CGRect(
            width - _contentInsets.Right - ChevronWidth,
            (height - ChevronWidth) / 2,
            ChevronWidth,
            ChevronWidth);

        if (_content is null)
            return;

        var contentWidth = width - _contentInsets.Left - _contentInsets.Right - ChevronWidth - ChevronSpacing;
        if (_content is MauiViewHost host)
            host.LayoutWidth = contentWidth;

        _content.Frame = new CGRect(
            _contentInsets.Left,
            _contentInsets.Top,
            Math.Max(0, contentWidth),
            Math.Max(0, height - _contentInsets.Top - _contentInsets.Bottom));
    }

    private void OnHover(UIHoverGestureRecognizer recognizer)
    {
        var hovering = recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed;
        BackgroundColor = hovering && !IsOpen
            ? UIColor.TertiarySystemBackground
            : UIColor.SecondarySystemBackground;
    }
}
