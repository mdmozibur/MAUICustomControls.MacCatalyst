using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using CoreText;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// MAUI's FontImageSource images (a Button's icon-font ImageSource) drawn through
/// <see cref="GlyphImageRenderer"/>: the glyph sits where MAUI puts it, but the image also holds ink
/// that reaches the edges of the line box.
/// </summary>
/// <remarks>
/// MAUI draws the glyph into a bitmap exactly the line box's size, and on macOS 26 and later centres
/// the glyph's ink in it. The app's icon font fills the line box with some icons (the toolbar's zoom
/// in and zoom out circles: ink 15.8 pt tall in a 16 pt box), so their antialiased edge fell outside
/// the bitmap and the circles looked cut at the top. The image here has room for that edge, and its
/// alignment rect insets keep the line box as its layout size; a glyph well inside its box gets the
/// same bitmap as before. Registered for IFontImageSource in MauiProgram; buttons measure with
/// <see cref="GlyphButtonHandler"/> so that UIKit lays the image out by its alignment rect.
/// </remarks>
public sealed class GlyphFontImageSourceService(IFontManager fontManager) : ImageSourceService, IImageSourceService<IFontImageSource>
{
    public override Task<IImageSourceServiceResult<UIImage>?> GetImageAsync(IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default) =>
        GetImageAsync((IFontImageSource)imageSource, scale, cancellationToken);

    public Task<IImageSourceServiceResult<UIImage>?> GetImageAsync(IFontImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        if (imageSource.IsEmpty || string.IsNullOrWhiteSpace(imageSource.Glyph))
        {
            return Task.FromResult<IImageSourceServiceResult<UIImage>?>(null);
        }

        var glyph = new NSAttributedString(imageSource.Glyph, new UIStringAttributes
        {
            Font = fontManager.GetFont(imageSource.Font),
            ForegroundColor = (imageSource.Color ?? Microsoft.Maui.Graphics.Colors.White).ToPlatform(),
        });
        var layoutSize = glyph.Size;
        if (layoutSize.Width <= 0 || layoutSize.Height <= 0)
        {
            return Task.FromResult<IImageSourceServiceResult<UIImage>?>(null);
        }

        // As MAUI: tinted by the button unless the source has its own colour.
        var renderingMode = imageSource.Color is null ? UIImageRenderingMode.Automatic : UIImageRenderingMode.AlwaysOriginal;
        var image = OperatingSystem.IsMacCatalystVersionAtLeast(26)
            ? RenderInkCentred(glyph, layoutSize, renderingMode)
            : GlyphImageRenderer.Render(glyph, layoutSize, renderingMode);
        IImageSourceServiceResult<UIImage> result = new ImageSourceServiceResult(image, resolutionDependent: true, () => image.Dispose());
        return Task.FromResult<IImageSourceServiceResult<UIImage>?>(result);
    }

    // MAUI's placement on macOS 26 and later: the glyph's ink centred in the line box.
    private static UIImage RenderInkCentred(NSAttributedString glyph, CGSize layoutSize, UIImageRenderingMode renderingMode)
    {
        using var line = new CTLine(glyph);
        var path = line.GetBounds(CTLineBoundsOptions.UseGlyphPathBounds);
        if (path.Width <= 0 || path.Height <= 0)
        {
            return GlyphImageRenderer.Render(glyph, layoutSize, renderingMode);
        }

        var ink = new CGRect((layoutSize.Width - path.Width) / 2, (layoutSize.Height - path.Height) / 2, path.Width, path.Height);
        return GlyphImageRenderer.Render(layoutSize, ink, renderingMode, () =>
        {
            var context = UIGraphics.GetCurrentContext();
            context.SaveState();
            context.TextMatrix = CGAffineTransform.MakeIdentity();
            context.TranslateCTM(0, layoutSize.Height);
            context.ScaleCTM(1, -1);
            context.TextPosition = new CGPoint(layoutSize.Width / 2 - path.GetMidX(), layoutSize.Height / 2 - path.GetMidY());
            line.Draw(context);
            context.RestoreState();
        });
    }
}

/// <summary>
/// MAUI's ButtonHandler, measured as the app's other glyph buttons are: UIKit lays a button's image
/// out by its alignment rect only when the size it measures against is bounded, so an unbounded
/// constraint (a button in a horizontal stack) would grow the button by an icon's ink margin
/// (<see cref="GlyphFontImageSourceService"/>, <see cref="ApplyGlyphTitle"/>).
/// </summary>
public sealed class GlyphButtonHandler : ButtonHandler
{
    // Buttons whose glyph title is shown as an image (see ApplyGlyphTitle).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIButton, object> GlyphTitled = new();

    /// <summary>
    /// A button whose title is one icon-font glyph (UWP's Button holding a FontIcon converts to
    /// Text + FontFamily) shows the glyph as an image drawn by <see cref="GlyphImageRenderer"/>,
    /// tinted with the title colour. UIKit draws a title into a label exactly the line box tall, and
    /// the icon font's round glyphs touch that box: the toolbar's zoom circles were cut at the top.
    /// The image keeps the line box as its layout size. Mapped after MAUI's Text, Font and
    /// TextColor (MauiProgram).
    /// </summary>
    public static void ApplyGlyphTitle(IButtonHandler handler)
    {
        if (handler.VirtualView is not Microsoft.Maui.Controls.Button button || handler.PlatformView is not { } platformButton)
        {
            return;
        }

        var glyphTitled = GlyphTitled.TryGetValue(platformButton, out _);
        if (button.ImageSource is not null || !IsIconGlyph(button) || handler.MauiContext is null)
        {
            if (glyphTitled)
            {
                // No longer a glyph: MAUI has set the title again; the glyph image goes.
                GlyphTitled.Remove(platformButton);
                platformButton.SetImage(null, UIControlState.Normal);
                platformButton.SetTitle(button.Text, UIControlState.Normal);
            }

            return;
        }

        // The symbol theme drew this glyph as an SF Symbol already (SymbolThemeIconService).
        if (!glyphTitled && string.IsNullOrEmpty(platformButton.Title(UIControlState.Normal)) && platformButton.ImageForState(UIControlState.Normal) is not null)
        {
            return;
        }

        var tint = platformButton.TitleColor(UIControlState.Normal) ?? platformButton.TintColor ?? UIColor.Label;
        // The font MAUI gave the title (scaled with the system's text size, as the title was drawn).
        var font = platformButton.TitleLabel?.Font ??
            handler.MauiContext.Services.GetRequiredService<IFontManager>().GetFont(((Microsoft.Maui.ITextStyle)button).Font);
        var glyph = new NSAttributedString(button.Text, new UIStringAttributes { Font = font, ForegroundColor = UIColor.White });
        var layoutSize = glyph.Size;
        if (layoutSize.Width <= 0 || layoutSize.Height <= 0)
        {
            return;
        }

        GlyphTitled.AddOrUpdate(platformButton, true);
        platformButton.SetImage(GlyphImageRenderer.Render(glyph, layoutSize, UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
        platformButton.SetTitle(string.Empty, UIControlState.Normal);
        platformButton.TintColor = tint;
    }

    // One character (one text element) in a font of the app's own (an icon font), not the system's.
    private static bool IsIconGlyph(Microsoft.Maui.Controls.Button button) =>
        !string.IsNullOrWhiteSpace(button.FontFamily) &&
        button.Text is { Length: > 0 } text &&
        System.Globalization.StringInfo.ParseCombiningCharacters(text).Length == 1 &&
        !char.IsWhiteSpace(text, 0);

    public override Microsoft.Maui.Graphics.Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        var size = base.GetDesiredSize(
            GlyphImageRenderer.LayoutConstraint(widthConstraint),
            GlyphImageRenderer.LayoutConstraint(heightConstraint));
        return new Microsoft.Maui.Graphics.Size(Math.Min(size.Width, widthConstraint), Math.Min(size.Height, heightConstraint));
    }
}
