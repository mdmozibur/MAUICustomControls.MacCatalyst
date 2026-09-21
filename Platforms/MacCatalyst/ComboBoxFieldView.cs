using System.Linq;
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
    private const double MinimumContentHeight = 15;

    private readonly UIImageView _chevron;
    private UIView? _content;
    private bool _isOpen;
    private UIEdgeInsets _contentInsets = new(4,4,4,4);

    public ComboBoxFieldView()
    {
        Layer.CornerRadius = 8;
        Layer.BorderWidth = 1;
        UpdateBorderColor();
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
            UpdateBorderColor();
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

    /// <summary>The element whose UseSystemFocusVisuals decides whether the focus ring is drawn.</summary>
    public Microsoft.Maui.Controls.BindableObject? FocusVisualsOwner { get; set; }

    // A plain UIControl takes no keyboard focus; the field is a control the user tabs to, like the
    // popup button it stands in for, and opens with Space or Return.
    public override bool CanBecomeFocused => Enabled && UserInteractionEnabled;

    public override void PressesBegan(Foundation.NSSet<UIPress> presses, UIPressesEvent evt)
    {
        if (Focused && presses.ToArray().Any(press => press.Key?.KeyCode is UIKeyboardHidUsage.KeyboardSpacebar or UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKeyboardHidUsage.KeypadEnter))
        {
            SendActionForControlEvents(UIControlEvent.TouchUpInside);
            return;
        }

        base.PressesBegan(presses, evt);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        FocusRing.UpdateCustomView(this, FocusVisualsOwner);

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

    // A layer's CGColor is resolved once, so the dynamic colours are re-resolved whenever the
    // appearance (light/dark) or the tint (the system accent, which marks the open field) changes.
    private void UpdateBorderColor() =>
        Layer.BorderColor = (_isOpen ? TintColor : UIColor.Separator).GetResolvedColor(TraitCollection).CGColor;

    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        UpdateBorderColor();
    }

    public override void TintColorDidChange()
    {
        base.TintColorDidChange();
        UpdateBorderColor();
    }

    private void OnHover(UIHoverGestureRecognizer recognizer)
    {
        var hovering = recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed;
        BackgroundColor = hovering && !IsOpen
            ? UIColor.TertiarySystemBackground
            : UIColor.SecondarySystemBackground;
    }
}
