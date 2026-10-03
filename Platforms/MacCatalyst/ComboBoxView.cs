using CoreGraphics;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The combo box as laid out on screen: an optional header above the field.
/// </summary>
/// <remarks>
/// The header sits outside <see cref="ComboBoxFieldView"/> so that the border, the hover state,
/// the focus ring and the click target all stay the field's own, as in WinUI, where a click on
/// the header does not open the list.
/// </remarks>
public sealed class ComboBoxView : UIView
{
    /// <summary>Gap between the header and the field.</summary>
    private const double HeaderSpacing = 4;

    private UIView? _header;

    public ComboBoxView()
    {
        Field = new ComboBoxFieldView();
        AddSubview(Field);
    }

    public ComboBoxFieldView Field { get; }

    /// <summary>Swaps in the view drawn above the field, or removes it.</summary>
    public void SetHeader(UIView? header)
    {
        _header?.RemoveFromSuperview();
        _header = header;

        if (header is not null)
            AddSubview(header);

        InvalidateIntrinsicContentSize();
        SetNeedsLayout();
    }

    public override CGSize SizeThatFits(CGSize size)
    {
        var header = MeasureHeader(size.Width);
        var headerHeight = header.Height > 0 ? (double)header.Height + HeaderSpacing : 0;

        var fieldHeight = double.IsInfinity(size.Height) ? size.Height : Math.Max(0, size.Height - headerHeight);
        var field = Field.SizeThatFits(new CGSize(size.Width, fieldHeight));

        return new CGSize(Math.Max((double)field.Width, (double)header.Width), (double)field.Height + headerHeight);
    }

    public override CGSize IntrinsicContentSize => SizeThatFits(new CGSize(NoIntrinsicMetric, NoIntrinsicMetric));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var width = Bounds.Width;
        var header = MeasureHeader(width);
        var headerHeight = header.Height > 0 ? (double)header.Height + HeaderSpacing : 0;

        if (_header is not null)
        {
            if (_header is MauiViewHost host)
                host.LayoutWidth = width;

            _header.Frame = new CGRect(0, 0, width, header.Height);
        }

        Field.Frame = new CGRect(0, headerHeight, width, Math.Max(0, (double)Bounds.Height - headerHeight));
    }

    private CGSize MeasureHeader(nfloat width)
    {
        if (_header is null)
            return CGSize.Empty;

        var bounded = width > 0 && !double.IsInfinity(width);
        if (_header is MauiViewHost host)
            host.LayoutWidth = bounded ? width : 0;

        return _header.SizeThatFits(new CGSize(bounded ? width : nfloat.MaxValue, nfloat.MaxValue));
    }
}
