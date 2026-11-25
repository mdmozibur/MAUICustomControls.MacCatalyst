using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>Makes and rebinds the views that show the drop-down's items.</summary>
internal interface IComboBoxRowFactory
{
    /// <summary>Items with the same key can share a view (same template, or both text rows).</summary>
    string ReuseKey(object item);

    /// <summary>A new view of the item's kind, already showing the item.</summary>
    UIView Create(object item);

    /// <summary>Points a view made for this item's kind at the item.</summary>
    void Bind(UIView view, object item);
}

/// <summary>
/// The drop-down list, presented under the field (see <see cref="DropdownPresentationController"/>).
/// </summary>
/// <remarks>
/// Rows are recycled table cells: only the rows on screen get an item view, and a cell scrolled
/// away is rebound to the next item rather than rebuilt. Building every row up front cost a MAUI
/// view, its handler and a layout pass per item, which made a box with thousands of entries (the
/// layers of a large drawing) visibly slow to open.
///
/// Row heights are measured lazily, per row, with one off-screen view per kind of row. The list's
/// width grows past a narrow field to its widest row, as WinUI's does; like WinUI, which sizes the
/// popup by the rows it has realized, a long list measures a bounded sample (the first rows and
/// those around the selection) rather than every item.
/// </remarks>
internal sealed class ComboBoxDropdownViewController : UIViewController, IUITableViewDataSource, IUITableViewDelegate
{
    /// <summary>Rows measured for the list's width: all of a short list, a sample of a long one.</summary>
    private const int WidthSampleSize = 150;

    /// <summary>Rows either side of the selection included in that sample.</summary>
    private const int WidthSampleAroundSelection = 20;

    private readonly IReadOnlyList<object> _items;
    private readonly IComboBoxRowFactory _rows;
    private readonly Action<int> _onSelect;
    private readonly Action _onCancel;
    private readonly double _maxWidth;
    private readonly double _maxHeight;
    private readonly double[] _rowHeights;
    private readonly Dictionary<string, UIView> _measurers = [];
    private double _width;

    private UITableView _table = null!;
    private int _highlightedIndex;
    private bool _scrolledToSelection;

    public ComboBoxDropdownViewController(
        IReadOnlyList<object> items,
        IComboBoxRowFactory rows,
        int selectedIndex,
        double width,
        double maxWidth,
        double maxHeight,
        Action<int> onSelect,
        Action onCancel)
    {
        _items = items;
        _rows = rows;
        _onSelect = onSelect;
        _onCancel = onCancel;
        _width = width;
        _maxWidth = Math.Max(width, maxWidth);
        _maxHeight = maxHeight;
        _highlightedIndex = selectedIndex;
        _rowHeights = new double[items.Count];
        Array.Fill(_rowHeights, double.NaN);
        SelectedIndex = selectedIndex;
    }

    public int SelectedIndex { get; }

    private double ContentWidth => Math.Max(0, _width - (ComboBoxRowView.HorizontalPadding * 2));

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // The list is as wide as its field, and grows - up to the cap - until its widest row fits,
        // as a WinUI drop-down does. A narrow field would otherwise cut its own entries short.
        if (_items.Count > 0)
            _width = Math.Min(_maxWidth, Math.Max(_width, MeasureWidestRow()));

        _table = new UITableView(CGRect.Empty, UITableViewStyle.Plain)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            BackgroundColor = UIColor.SecondarySystemBackground,
            ClipsToBounds = true,
            SeparatorStyle = UITableViewCellSeparatorStyle.SingleLine,
            SeparatorColor = UIColor.Separator,
            SeparatorInset = UIEdgeInsets.Zero,
            // No filler separators under the last row of a short list.
            TableFooterView = new UIView(CGRect.Empty),
            CellLayoutMarginsFollowReadableWidth = false,
            EstimatedRowHeight = (nfloat)EstimatedRowHeight(),
            SectionHeaderTopPadding = 0,
            DataSource = this,
            Delegate = this,
        };
        _table.Layer.CornerRadius = 10;
        _table.Layer.BorderWidth = 1;
        _table.Layer.BorderColor = UIColor.Separator.CGColor;

        // Shadow lives on the unclipped root; the table does the corner clipping.
        View!.BackgroundColor = UIColor.Clear;
        View.ClipsToBounds = false;
        View.Layer.ShadowColor = UIColor.Black.CGColor;
        View.Layer.ShadowOpacity = 0.18f;
        View.Layer.ShadowRadius = 8;
        View.Layer.ShadowOffset = new CGSize(0, 4);

        View.AddSubview(_table);

        NSLayoutConstraint.ActivateConstraints([
            _table.TopAnchor.ConstraintEqualTo(View.TopAnchor),
            _table.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
            _table.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            _table.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
        ]);

        PreferredContentSize = CalculateContentSize();
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();

        // Once the table has its final frame: bring the selection into view, centred, as WinUI does.
        if (_scrolledToSelection || _table.Bounds.Height <= 0)
            return;

        _scrolledToSelection = true;
        if (_highlightedIndex >= 0 && _highlightedIndex < _items.Count)
            _table.ScrollToRow(NSIndexPath.FromRowSection(_highlightedIndex, 0), UITableViewScrollPosition.Middle, animated: false);
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

    // ---- Table data source and delegate ----

    public nint RowsInSection(UITableView tableView, nint section) => _items.Count;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var index = indexPath.Row;
        var item = _items[index];
        var key = _rows.ReuseKey(item);

        if (tableView.DequeueReusableCell(key) is ComboBoxRowView cell && cell.ItemView is { } view)
        {
            _rows.Bind(view, item);
        }
        else
        {
            cell = new ComboBoxRowView(key, OnRowHovered) { ItemView = _rows.Create(item) };
        }

        cell.Index = index;
        cell.IsSelectedItem = index == SelectedIndex;
        cell.IsHighlighted = index == _highlightedIndex;
        cell.SetNeedsLayout();
        return cell;
    }

    [Export("tableView:heightForRowAtIndexPath:")]
    public nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath) => (nfloat)RowHeight(indexPath.Row);

    [Export("tableView:didSelectRowAtIndexPath:")]
    public void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
        tableView.DeselectRow(indexPath, animated: false);
        _onSelect(indexPath.Row);
    }

    // ---- Measuring ----

    /// <summary>An off-screen view of the item's kind, pointed at the item, for measuring.</summary>
    private UIView Measurer(object item)
    {
        var key = _rows.ReuseKey(item);
        if (_measurers.TryGetValue(key, out var measurer))
        {
            _rows.Bind(measurer, item);
            return measurer;
        }

        measurer = _rows.Create(item);
        _measurers[key] = measurer;
        return measurer;
    }

    private double RowHeight(int index)
    {
        if (index < 0 || index >= _rowHeights.Length)
            return 0;

        if (!double.IsNaN(_rowHeights[index]))
            return _rowHeights[index];

        var measurer = Measurer(_items[index]);
        var width = ContentWidth;
        if (measurer is MauiViewHost host)
            host.LayoutWidth = width;

        var height = ComboBoxRowView.RowHeight(
            (double)measurer.SizeThatFits(new CGSize(width, double.PositiveInfinity)).Height);
        _rowHeights[index] = height;
        return height;
    }

    private double NaturalRowWidth(int index)
    {
        var measurer = Measurer(_items[index]);
        var width = measurer is MauiViewHost host
            ? host.NaturalWidth
            : (double)measurer.SizeThatFits(new CGSize(nfloat.MaxValue, nfloat.MaxValue)).Width;
        return ComboBoxRowView.RowWidth(width);
    }

    private double MeasureWidestRow()
    {
        var widest = 0d;
        foreach (var index in WidthSample())
            widest = Math.Max(widest, NaturalRowWidth(index));
        return widest;
    }

    /// <summary>Every row of a short list; the first rows and those around the selection of a long one.</summary>
    private IEnumerable<int> WidthSample()
    {
        var count = _items.Count;
        if (count <= WidthSampleSize)
        {
            for (var i = 0; i < count; i++)
                yield return i;
            yield break;
        }

        var head = WidthSampleSize - (WidthSampleAroundSelection * 2) - 1;
        for (var i = 0; i < head; i++)
            yield return i;

        if (SelectedIndex < 0)
            yield break;

        var from = Math.Max(head, SelectedIndex - WidthSampleAroundSelection);
        var to = Math.Min(count - 1, SelectedIndex + WidthSampleAroundSelection);
        for (var i = from; i <= to; i++)
            yield return i;
    }

    private double EstimatedRowHeight() =>
        _items.Count == 0 ? 28 : RowHeight(SelectedIndex >= 0 && SelectedIndex < _items.Count ? SelectedIndex : 0);

    /// <summary>The list's size: rows are measured only until the height cap is reached.</summary>
    private CGSize CalculateContentSize()
    {
        var separator = 1d / (double)UIScreen.MainScreen.Scale;
        var height = 0d;

        for (var i = 0; i < _items.Count && height < _maxHeight; i++)
            height += RowHeight(i) + (i < _items.Count - 1 ? separator : 0);

        return new CGSize(_width, Math.Min(height, _maxHeight));
    }

    // ---- Highlight ----

    private void OnRowHovered(ComboBoxRowView row, bool hovering)
    {
        if (hovering)
            Highlight(row.Index, scrollIntoView: false);
        else
            row.IsHighlighted = false;
    }

    private void Highlight(int index, bool scrollIntoView)
    {
        if (index < 0 || index >= _items.Count)
            return;

        _highlightedIndex = index;

        foreach (var cell in _table.VisibleCells)
        {
            if (cell is ComboBoxRowView row)
                row.IsHighlighted = row.Index == index;
        }

        if (scrollIntoView)
            _table.ScrollToRow(NSIndexPath.FromRowSection(index, 0), UITableViewScrollPosition.None, animated: false);
    }
}
