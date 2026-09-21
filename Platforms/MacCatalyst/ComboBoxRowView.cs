using CoreGraphics;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>One row of the drop-down, wrapping the caller's templated view.</summary>
internal sealed class ComboBoxRowView : UIControl
{
    private const double HorizontalPadding = 12;
    private const double VerticalPadding = 6;

    private readonly UIView _content;
    private readonly bool _isSelected;
    private readonly Action _onActivated;
    private readonly Action _onHovered;
    private readonly double _width;

    private bool _isHighlighted;

    public ComboBoxRowView(UIView content, bool isSelected, double width, Action onActivated, Action onHovered)
    {
        _content = content;
        _isSelected = isSelected;
        _onActivated = onActivated;
        _onHovered = onHovered;
        _width = width;

        AddSubview(content);
        UpdateBackground();

        TouchUpInside += (_, _) => _onActivated();
        AddGestureRecognizer(new UIHoverGestureRecognizer(OnHover));
    }

    /// <summary>Set by keyboard navigation and by hover; distinct from being the selected item.</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            _isHighlighted = value;
            UpdateBackground();
        }
    }

    public override CGSize SizeThatFits(CGSize size)
    {
        var width = size.Width > 0 && !double.IsInfinity(size.Width) ? (double)size.Width : _width;
        var available = width - (HorizontalPadding * 2);

        if (_content is MauiViewHost host)
            host.LayoutWidth = available;

        var contentSize = _content.SizeThatFits(new CGSize(available, double.PositiveInfinity));
        return new CGSize(width, contentSize.Height + (VerticalPadding * 2));
    }

    public override CGSize IntrinsicContentSize => SizeThatFits(new CGSize(_width, NoIntrinsicMetric));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var contentWidth = Math.Max(0, (double)Bounds.Width - (HorizontalPadding * 2));

        if (_content is MauiViewHost host)
            host.LayoutWidth = contentWidth;

        _content.Frame = new CGRect(
            HorizontalPadding,
            VerticalPadding,
            contentWidth,
            Math.Max(0, (double)Bounds.Height - (VerticalPadding * 2)));
    }

    private void OnHover(UIHoverGestureRecognizer recognizer)
    {
        if (recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed)
            _onHovered();
        else
            IsHighlighted = false;
    }

    // Highlight and selection take the tint: the system accent, as WinUI's list selection does.
    private void UpdateBackground() =>
        BackgroundColor = _isHighlighted
            ? TintColor.ColorWithAlpha(0.18f)
            : _isSelected
                ? TintColor.ColorWithAlpha(0.10f)
                : UIColor.Clear;

    public override void TintColorDidChange()
    {
        base.TintColorDidChange();
        UpdateBackground();
    }
}
