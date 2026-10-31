using CoreGraphics;
using Foundation;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Icon-font glyphs drawn into images for native buttons, segments and menus.
/// </summary>
/// <remarks>
/// A glyph's ink can reach past its line box (font size tall, advance wide): the app's icon font
/// draws circles that fill the box exactly and arrows wider than the advance. An image the size of
/// the line box cut those edges off, while UWP draws the overflow. The image here also holds the
/// ink beyond the line box, and its alignment rect insets keep the line box as the size UIKit lays
/// the image out by, so buttons measure as before.
/// </remarks>
internal static class GlyphImageRenderer
{
    // Room for the antialiased edge of ink that touches the line box.
    private const double SmoothingMargin = 1;

    // Any constraint larger than a window; see LayoutConstraint.
    private const double UnboundedLength = 100_000;

    /// <summary>
    /// A measuring constraint for a view showing these images. UIKit lays an image out by its
    /// alignment rect only when the size it measures against is bounded on that axis; against an
    /// unbounded one (MAUI's infinite height in a stack) it uses the whole image, and a button would
    /// grow by the ink overhang. Measure with this instead of infinity.
    /// </summary>
    public static nfloat LayoutConstraint(double constraint) =>
        (nfloat)(double.IsFinite(constraint) ? constraint : UnboundedLength);

    /// <summary>The glyph's line box, centred in an image laid out at <paramref name="layoutSize"/>.</summary>
    public static UIImage Render(NSAttributedString glyph, CGSize layoutSize, UIImageRenderingMode renderingMode)
    {
        var lineSize = glyph.Size;
        var origin = new CGPoint(
            Math.Max((layoutSize.Width - lineSize.Width) / 2, 0),
            Math.Max((layoutSize.Height - lineSize.Height) / 2, 0));
        return Render(layoutSize, InkBounds(glyph, origin), renderingMode, () => glyph.DrawString(origin));
    }

    /// <summary>
    /// An image laid out at <paramref name="layoutSize"/> that <paramref name="draw"/> fills, in
    /// layout coordinates; <paramref name="ink"/> is what it draws, which may lie outside.
    /// </summary>
    public static UIImage Render(CGSize layoutSize, CGRect ink, UIImageRenderingMode renderingMode, Action draw)
    {
        var overhang = new UIEdgeInsets(
            Overhang(-ink.Top),
            Overhang(-ink.Left),
            Overhang(ink.Bottom - layoutSize.Height),
            Overhang(ink.Right - layoutSize.Width));
        var imageSize = new CGSize(
            layoutSize.Width + overhang.Left + overhang.Right,
            layoutSize.Height + overhang.Top + overhang.Bottom);

        var image = new UIGraphicsImageRenderer(imageSize).CreateImage(context =>
        {
            context.CGContext.TranslateCTM(overhang.Left, overhang.Top);
            draw();
        });

        image = image.ImageWithRenderingMode(renderingMode);
        return overhang.Equals(UIEdgeInsets.Zero) ? image : image.ImageWithAlignmentRectInsets(overhang);
    }

    /// <summary>Where <paramref name="text"/> drawn at <paramref name="origin"/> puts ink.</summary>
    public static CGRect InkBounds(NSAttributedString text, CGPoint origin)
    {
        var ink = text.GetBoundingRect(
            new CGSize(nfloat.MaxValue, nfloat.MaxValue),
            NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesDeviceMetrics,
            null);
        ink.Offset(origin.X, origin.Y);
        return ink;
    }

    // Ink this far past an edge (negative: inside it) needs that much more room, plus the margin
    // once it comes within the margin of the edge. Whole points, so the line box stays pixel-aligned.
    private static nfloat Overhang(nfloat beyondEdge) =>
        beyondEdge + SmoothingMargin > 0 ? (nfloat)Math.Ceiling(beyondEdge + SmoothingMargin) : 0;
}
