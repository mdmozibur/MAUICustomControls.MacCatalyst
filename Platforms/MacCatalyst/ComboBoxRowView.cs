using CoreGraphics;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// One row of the drop-down: a recycled table cell wrapping the item's view (its template, or its
/// text), which is created once per cell and rebound as the cell is reused for other items.
/// </summary>
internal sealed class ComboBoxRowView : UITableViewCell
{
    public const double HorizontalPadding = 12;
    public const double VerticalPadding = 6;

    private readonly Action<ComboBoxRowView, bool> _onHovered;
    private UIView? _content;
    private bool _isSelectedItem;
    private bool _isHighlighted;

    public ComboBoxRowView(string reuseIdentifier, Action<ComboBoxRowView, bool> onHovered)
        : base(UITableViewCellStyle.Default, reuseIdentifier)
    {
        _onHovered = onHovered;

        // The row draws its own selection and highlight, in the accent colour as WinUI does; the
        // table's own selection look stays off.
        SelectionStyle = UITableViewCellSelectionStyle.None;
        BackgroundColor = UIColor.Clear;
        AccessibilityTraits = UIAccessibilityTrait.Button;

        AddGestureRecognizer(new UIHoverGestureRecognizer(OnHover));
    }

    /// <summary>The item index this cell currently shows.</summary>
    public int Index { get; set; } = -1;

    /// <summary>The item's view; set once, when the cell is first created.</summary>
    public UIView? ItemView
    {
        get => _content;
        set
        {
            _content?.RemoveFromSuperview();
            _content = value;
            if (value is not null)
                ContentView.AddSubview(value);
            SetNeedsLayout();
        }
    }

    /// <summary>Whether this row is the box's selected item.</summary>
    public bool IsSelectedItem
    {
        get => _isSelectedItem;
        set
        {
            _isSelectedItem = value;
            UpdateBackground();
            AccessibilityTraits = value
                ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected
                : UIAccessibilityTrait.Button;
        }
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

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        if (_content is null)
            return;

        var bounds = ContentView.Bounds;
        var contentWidth = Math.Max(0, (double)bounds.Width - (HorizontalPadding * 2));

        if (_content is MauiViewHost host)
            host.LayoutWidth = contentWidth;

        _content.Frame = new CGRect(
            HorizontalPadding,
            VerticalPadding,
            contentWidth,
            Math.Max(0, (double)bounds.Height - (VerticalPadding * 2)));
        _content.SetNeedsLayout();
    }

    private void OnHover(UIHoverGestureRecognizer recognizer) =>
        _onHovered(this, recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed);

    private void UpdateBackground() =>
        ContentView.BackgroundColor = _isHighlighted
            ? TintColor.ColorWithAlpha(0.18f)
            : _isSelectedItem
                ? TintColor.ColorWithAlpha(0.10f)
                : UIColor.Clear;

    public override void TintColorDidChange()
    {
        base.TintColorDidChange();
        UpdateBackground();
    }

    /// <summary>The height a row needs around an item view of the given height.</summary>
    public static double RowHeight(double contentHeight) => Math.Ceiling(contentHeight) + (VerticalPadding * 2);

    /// <summary>The width a row needs around an item view of the given width.</summary>
    public static double RowWidth(double contentWidth) => Math.Ceiling(contentWidth) + (HorizontalPadding * 2);
}
