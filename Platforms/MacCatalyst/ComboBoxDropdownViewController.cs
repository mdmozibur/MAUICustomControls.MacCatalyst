using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The drop-down list, presented as a popover anchored to the field.
/// </summary>
/// <remarks>
/// A popover is used deliberately: it anchors to the source view, floats above all
/// other content, is never clipped by an ancestor, and dismisses on an outside click —
/// all of which would otherwise have to be hand-built. On Mac Catalyst it also renders
/// as a genuine Mac popover.
///
/// Rows live in a stack view rather than a UICollectionView: there is no view recycling,
/// which keeps templated-view lifetime simple, at the cost of building every row up front.
/// Fine into the hundreds of items; swap in a UICollectionView beyond that.
/// </remarks>
internal sealed class ComboBoxDropdownViewController : UIViewController
{
    private readonly IReadOnlyList<object> _items;
    private readonly Func<object, UIView> _rowFactory;
    private readonly Action<int> _onSelect;
    private readonly Action _onCancel;
    private readonly double _maxWidth;
    private readonly double _maxHeight;
    private double _width;
    private readonly List<ComboBoxRowView> _rows = [];

    private UIScrollView _scrollView = null!;
    private UIStackView _stack = null!;
    private int _highlightedIndex;

    public ComboBoxDropdownViewController(
        IReadOnlyList<object> items,
        Func<object, UIView> rowFactory,
        int selectedIndex,
        double width,
        double maxWidth,
        double maxHeight,
        Action<int> onSelect,
        Action onCancel)
    {
        _items = items;
        _rowFactory = rowFactory;
        _onSelect = onSelect;
        _onCancel = onCancel;
        _width = width;
        _maxWidth = Math.Max(width, maxWidth);
        _maxHeight = maxHeight;
        _highlightedIndex = selectedIndex;
        SelectedIndex = selectedIndex;
    }

    public int SelectedIndex { get; }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        _stack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.EqualSpacing,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        for (var i = 0; i < _items.Count; i++)
        {
            var index = i;
            var row = new ComboBoxRowView(
                _rowFactory(_items[i]),
                isSelected: i == SelectedIndex,
                width: _width,
                onActivated: () => _onSelect(index),
                onHovered: () => Highlight(index, scrollIntoView: false));

            _rows.Add(row);
            _stack.AddArrangedSubview(row);

            if (i < _items.Count - 1)
                _stack.AddArrangedSubview(CreateSeparator());
        }

        // The list is as wide as its field, and grows - up to the cap - until its widest row fits,
        // as a WinUI drop-down does. A narrow field would otherwise cut its own entries short.
        if (_rows.Count > 0)
        {
            _width = Math.Min(_maxWidth, Math.Max(_width, _rows.Max(static row => row.NaturalWidth)));
            foreach (var row in _rows)
                row.PreferredWidth = _width;
        }

        _scrollView = new UIScrollView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            BackgroundColor = UIColor.SecondarySystemBackground,
            ClipsToBounds = true
        };
        _scrollView.Layer.CornerRadius = 10;
        _scrollView.Layer.BorderWidth = 1;
        _scrollView.Layer.BorderColor = UIColor.Separator.CGColor;
        _scrollView.AddSubview(_stack);

        // Shadow lives on the unclipped root; the scroll view does the corner clipping.
        View!.BackgroundColor = UIColor.Clear;
        View.ClipsToBounds = false;
        View.Layer.ShadowColor = UIColor.Black.CGColor;
        View.Layer.ShadowOpacity = 0.18f;
        View.Layer.ShadowRadius = 8;
        View.Layer.ShadowOffset = new CGSize(0, 4);

        View.AddSubview(_scrollView);

        NSLayoutConstraint.ActivateConstraints([
            _scrollView.TopAnchor.ConstraintEqualTo(View!.TopAnchor),
            _scrollView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
            _scrollView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            _scrollView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

            _stack.TopAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.TopAnchor),
            _stack.BottomAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.BottomAnchor),
            _stack.LeadingAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.LeadingAnchor),
            _stack.TrailingAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.TrailingAnchor),
            _stack.WidthAnchor.ConstraintEqualTo(_scrollView.FrameLayoutGuide.WidthAnchor)
        ]);

        PreferredContentSize = CalculateContentSize();
        Highlight(_highlightedIndex, scrollIntoView: true);
    }

    /// <summary>Mac users expect the keyboard to drive a drop-down.</summary>
    public override bool CanBecomeFirstResponder => true;

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        BecomeFirstResponder();
    }

    public override UIKeyCommand[] KeyCommands =>
    [
        UIKeyCommand.Create(UIKeyCommand.UpArrow, (UIKeyModifierFlags)0, new Selector("moveHighlightUp:")),
        UIKeyCommand.Create(UIKeyCommand.DownArrow, (UIKeyModifierFlags)0, new Selector("moveHighlightDown:")),
        UIKeyCommand.Create(new NSString("\r"), (UIKeyModifierFlags)0, new Selector("commitHighlighted:")),
        UIKeyCommand.Create(UIKeyCommand.Escape, (UIKeyModifierFlags)0, new Selector("dismissDropdown:"))
    ];

    [Export("moveHighlightUp:")]
    public void MoveHighlightUp(UIKeyCommand command) => Highlight(_highlightedIndex - 1, scrollIntoView: true);

    [Export("moveHighlightDown:")]
    public void MoveHighlightDown(UIKeyCommand command) => Highlight(_highlightedIndex + 1, scrollIntoView: true);

    [Export("commitHighlighted:")]
    public void CommitHighlighted(UIKeyCommand command)
    {
        if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count)
            _onSelect(_highlightedIndex);
    }

    [Export("dismissDropdown:")]
    public void DismissDropdown(UIKeyCommand command) => _onCancel();

    private CGSize CalculateContentSize()
    {
        var height = 0d;

        foreach (var view in _stack.ArrangedSubviews)
            height += view.SizeThatFits(new CGSize(_width, double.PositiveInfinity)).Height;

        return new CGSize(_width, Math.Min(height, _maxHeight));
    }

    private void Highlight(int index, bool scrollIntoView)
    {
        if (index < 0 || index >= _rows.Count)
            return;

        for (var i = 0; i < _rows.Count; i++)
            _rows[i].IsHighlighted = i == index;

        _highlightedIndex = index;

        if (scrollIntoView)
            _scrollView.ScrollRectToVisible(_rows[index].Frame, animated: false);
    }

    private static UIView CreateSeparator()
    {
        var separator = new UIView { BackgroundColor = UIColor.Separator };
        separator.HeightAnchor.ConstraintEqualTo(1f / UIScreen.MainScreen.Scale).Active = true;
        return separator;
    }
}
