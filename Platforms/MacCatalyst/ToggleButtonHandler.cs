
using CoreGraphics;
using CoreText;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Foundation;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleButtonHandler : ViewHandler<ToggleButton, UIButton>
{
    public static PropertyMapper<ToggleButton, ToggleButtonHandler> PropertyMapper = new(ViewMapper)
    {
        ["UseSystemFocusVisuals"] = (handler, view) => FocusRing.UpdateSystemControl(handler.PlatformView, view),
        [nameof(ToggleButton.Text)] = MapText,
        [nameof(ToggleButton.FontSize)] = MapFontSize,
        [nameof(ToggleButton.Padding)] = MapPadding,
        [nameof(ToggleButton.HorizontalContentAlignment)] = MapHorizontalContentAlignment,
        [nameof(ToggleButton.BorderThickness)] = MapBorderThickness,
        [nameof(ToggleButton.BorderBrush)] = MapBorderBrush,
        [nameof(ToggleButton.Foreground)] = MapColor,
        [nameof(ToggleButton.IconGlyph)] = MapImageSource,
        [nameof(ToggleButton.CustomFontFamily)] = MapImageSource,
        [nameof(ToggleButton.IsChecked)] = MapIsSelected,
        [nameof(ToggleButton.ImageSpacing)] = MapImageSpacing,
        [nameof(ToggleButton.Orientation)] = MapOrientation,
        // VoiceOver: a toggle with its checked state, named by its text or, for an icon-only button,
        // its tooltip. MAUI's own semantics mapping would clear the label, so it runs first.
        [nameof(IView.Semantics)] = (handler, view) =>
        {
            ViewHandler.MapSemantics(handler, view);
            if (!handler._settingVirtualView)
            {
                UpdateAccessibility(handler.PlatformView, view);
            }
        },
        ["ToolTip"] = (handler, view) =>
        {
            ViewHandler.MapToolTip(handler, view);
            if (!handler._settingVirtualView)
            {
                UpdateAccessibility(handler.PlatformView, view);
            }
        },
    };

    private PointerHoverTracker? _hover;

    // Set while MAUI maps every property of a new virtual view: the content and appearance
    // mappings each rebuild the button (configuration, glyph bitmap), so they are applied once when
    // SetVirtualView has mapped everything instead of once per property.
    private bool _settingVirtualView;

    public ToggleButtonHandler() : base(PropertyMapper) { }

    public override void SetVirtualView(IView view)
    {
        _settingVirtualView = true;
        try
        {
            base.SetVirtualView(view);
        }
        finally
        {
            _settingVirtualView = false;
        }

        if (PlatformView is { } button && VirtualView is { } toggle)
        {
            UpdateButtonContent(button, toggle);
            UpdateButtonAppearance(button, toggle);
        }
    }

    private void RefreshContent(ToggleButton view)
    {
        if (!_settingVirtualView && PlatformView is { } button)
        {
            UpdateButtonContent(button, view);
        }
    }

    private void RefreshAppearance(ToggleButton view)
    {
        if (!_settingVirtualView && PlatformView is { } button)
        {
            UpdateButtonAppearance(button, view);
        }
    }

    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        ConfigureButton(platformView);
        // The primary action (a click, VoiceOver's activate, Space/Return with keyboard focus); the
        // button has toggled Selected by the time it is sent.
        platformView.AddTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);

        // UWP ToggleButton's PointerOver and Pressed states: hover comes from a hover recognizer,
        // pressed from the button's Highlighted state, which re-runs the configuration update handler.
        _hover = new PointerHoverTracker(platformView, () => UpdateInteractionBackground(PlatformView, VirtualView));
        platformView.ConfigurationUpdateHandler = button => UpdateInteractionBackground(button, VirtualView);

        // The glyph is rasterized into a UIImage with the foreground color baked in, so it does
        // not follow the dynamic UIColor.Label the way the title does. Re-render on theme change.
        if (Application.Current is Application application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        RefreshContent(VirtualView);
        RefreshAppearance(VirtualView);
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (PlatformView is null || VirtualView is null)
        {
            return;
        }

        UpdateButtonContent(PlatformView, VirtualView);
        UpdateButtonAppearance(PlatformView, VirtualView);
    }

    private void ConfigureButton(UIButton button)
    {
        button.ChangesSelectionAsPrimaryAction = true;
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        button.ClipsToBounds = true;
        button.Layer.MasksToBounds = true;
        button.Layer.CornerRadius = 0;
        button.TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton();
        button.ChangesSelectionAsPrimaryAction = true;
        MacIdiomControlStyle.Apply(button);
        return button;
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        if (Application.Current is Application application)
        {
            application.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        platformView.RemoveTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);
        platformView.ConfigurationUpdateHandler = null;
        _hover?.Dispose();
        _hover = null;
        base.DisconnectHandler(platformView);
    }

    private void ButtonTapped(object? sender, EventArgs e)
    {
        if (VirtualView.IsChecked != PlatformView.Selected)
        {
            VirtualView.IsChecked = PlatformView.Selected;
        }

        UpdateButtonAppearance(PlatformView, VirtualView);
    }

    public static void MapIsSelected(ToggleButtonHandler handler, ToggleButton view)
    {
        if (handler.PlatformView.Selected != view.IsChecked)
        {
            handler.PlatformView.Selected = view.IsChecked;
        }

        handler.RefreshAppearance(view);
    }

    public static void MapText(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
        if (!handler._settingVirtualView)
        {
            UpdateAccessibility(handler.PlatformView, view);
        }
    }

    public static void MapFontSize(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
    }

    public static void MapPadding(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
    }

    public static void MapHorizontalContentAlignment(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
    }

    public static void MapBorderThickness(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshAppearance(view);
    }

    public static void MapBorderBrush(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshAppearance(view);
    }

    public static void MapColor(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
        handler.RefreshAppearance(view);
    }

    public static void MapImageSource(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.RefreshContent(view);
    }

    private static void MapImageSpacing(ToggleButtonHandler handler, ToggleButton button)
    {
        handler.RefreshContent(button);
    }

    private static void MapOrientation(ToggleButtonHandler handler, ToggleButton button)
    {
        handler.RefreshContent(button);
    }

    private static void UpdateButtonContent(UIButton button, ToggleButton view)
    {
        var configuration = button.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
        var foregroundColor = ResolveContentColor(view);

        var hasIcon = !string.IsNullOrWhiteSpace(view.IconGlyph);
        var titleFontSize = (nfloat)Math.Max(hasIcon ? Math.Min(view.FontSize * 0.75, 13.0) : view.FontSize, 8d);
        var titleFont = UIFont.SystemFontOfSize(titleFontSize);

        var image = CreateImage(view, foregroundColor);
        configuration.Title = view.Text;
        configuration.Image = image;
        configuration.ImagePlacement = ResolveImagePlacement(view.Orientation);
        configuration.ImagePadding = (nfloat)Math.Max(0, view.ImageSpacing);
        configuration.BaseForegroundColor = foregroundColor;
        configuration.ContentInsets = ResolveContentInsets(view.Padding);
        configuration.CornerStyle = UIButtonConfigurationCornerStyle.Fixed;
        configuration.Background.CornerRadius = 0;

        button.Configuration = configuration;
        button.SetTitle(view.Text, UIControlState.Normal);
        button.SetTitle(view.Text, UIControlState.Selected);
        button.SetImage(image, UIControlState.Normal);
        button.SetImage(image, UIControlState.Selected);
        button.HorizontalAlignment = ResolveContentHorizontalAlignment(view.HorizontalContentAlignment);

        var titleLabel = button.TitleLabel;
        if (titleLabel is not null)
        {
            titleLabel.Font = titleFont;
            titleLabel.Lines = view.Orientation == StackOrientation.Vertical ? 2 : 1;
            titleLabel.LineBreakMode = view.Orientation == StackOrientation.Vertical
                ? UILineBreakMode.WordWrap
                : UILineBreakMode.TailTruncation;
            titleLabel.TextAlignment = UITextAlignment.Center;
        }

        button.SetNeedsLayout();
    }

    private static void UpdateButtonAppearance(UIButton button, ToggleButton view)
    {
        var contentColor = ResolveContentColor(view);
        var accentColor = ResolveAccentColor(view);

        button.TintColor = contentColor;
        button.Layer.BorderWidth = (nfloat)view.BorderThickness.Left;
        button.Layer.BorderColor = ResolveBorderColor(view, accentColor).CGColor;
        button.Alpha = button.Enabled ? 1f : 0.55f;
        UpdateInteractionBackground(button, view);
        UpdateAccessibility(button, view);
    }

    private static void UpdateAccessibility(UIButton button, ToggleButton view) =>
        ButtonAccessibility.Update(button, view, isToggle: true, isOn: view.IsChecked, text: view.Text);

    // Unchecked, the template paints its root with ToggleButtonBackgroundPointerOver/Pressed over the
    // control's own background. Checked keeps this control's accent tint, a little stronger under the
    // pointer and lighter while pressed, as WinUI's AccentFillColorSecondary/Tertiary are.
    private static void UpdateInteractionBackground(UIButton? button, ToggleButton? view)
    {
        if (button is null || view is null)
        {
            return;
        }

        var hovered = (view.Handler as ToggleButtonHandler)?._hover?.IsHovered == true;
        var pressed = button.Highlighted;
        if (view.IsChecked)
        {
            var tint = !button.Enabled ? 0.16f : pressed ? 0.10f : hovered ? 0.24f : 0.16f;
            button.BackgroundColor = ResolveAccentColor(view).ColorWithAlpha(tint);
            return;
        }

        button.BackgroundColor = !button.Enabled
            ? ResolveBackgroundColor(view)
            : pressed
                ? InteractionColors.Pressed(InteractionColors.ToggleButtonPressedKey)
                : hovered ? InteractionColors.PointerOver(InteractionColors.ToggleButtonPointerOverKey) : ResolveBackgroundColor(view);
    }

    private static NSDirectionalEdgeInsets ResolveContentInsets(Thickness padding)
    {
        return new NSDirectionalEdgeInsets(
            (nfloat)Math.Max(0, padding.Top),
            (nfloat)Math.Max(0, padding.Left),
            (nfloat)Math.Max(0, padding.Bottom),
            (nfloat)Math.Max(0, padding.Right));
    }

    private static UIControlContentHorizontalAlignment ResolveContentHorizontalAlignment(LayoutOptions alignment)
    {
        return alignment.Alignment switch
        {
            LayoutAlignment.Start => UIControlContentHorizontalAlignment.Left,
            LayoutAlignment.Center => UIControlContentHorizontalAlignment.Center,
            LayoutAlignment.End => UIControlContentHorizontalAlignment.Right,
            LayoutAlignment.Fill => UIControlContentHorizontalAlignment.Fill,
            _ => UIControlContentHorizontalAlignment.Center,
        };
    }

    private static NSDirectionalRectEdge ResolveImagePlacement(StackOrientation orientation)
    {
        return orientation == StackOrientation.Vertical
            ? NSDirectionalRectEdge.Top
            : NSDirectionalRectEdge.Leading;
    }

    private static UIImage? CreateImage(ToggleButton view, UIColor tintColor)
    {
        if (string.IsNullOrWhiteSpace(view.IconGlyph))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(view.CustomFontFamily))
        {
            return CreateFontGlyphImage(view.IconGlyph, view.CustomFontFamily, view.FontSize, tintColor);
        }

        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var image = UIImage.GetSystemImage(view.IconGlyph, config);
        return image?.ApplyTintColor(tintColor, UIImageRenderingMode.AlwaysOriginal);
    }

    // Rendered glyphs by glyph, font, size and colour (the colour is baked into the bitmap). Buttons
    // share them: the left pane alone creates a few hundred, most with the same handful of glyphs.
    private static readonly Dictionary<(string Glyph, string FontFamily, double FontSize, nfloat Red, nfloat Green, nfloat Blue, nfloat Alpha), UIImage?> GlyphImages = new();

    private static UIImage? CreateFontGlyphImage(string glyph, string fontFamily, double fontSize, UIColor tintColor)
    {
        tintColor.GetRGBA(out var red, out var green, out var blue, out var alpha);
        var key = (glyph, fontFamily, fontSize, red, green, blue, alpha);
        if (!GlyphImages.TryGetValue(key, out var image))
        {
            image = RenderFontGlyphImage(glyph, fontFamily, fontSize, tintColor);
            GlyphImages[key] = image;
        }

        return image;
    }

    private static UIImage? RenderFontGlyphImage(string glyph, string fontFamily, double fontSize, UIColor tintColor)
    {
        var resolvedFontSize = (nfloat)Math.Max(fontSize > 0 ? fontSize : 16d, 8d);
        var font = ResolvePlatformFont(fontFamily, resolvedFontSize) ?? UIFont.SystemFontOfSize(resolvedFontSize);
        var attributes = new UIStringAttributes
        {
            Font = font,
            ForegroundColor = tintColor,
        };

        using var glyphText = new NSString(glyph);
        var textSize = glyphText.GetSizeUsingAttributes(attributes);
        var width = (nfloat)Math.Ceiling(Math.Max(textSize.Width, resolvedFontSize));
        var height = (nfloat)Math.Ceiling(Math.Max(textSize.Height, resolvedFontSize));
        var imageSize = new CGSize(width, height);

        var drawPoint = new CGPoint(
            Math.Max((width - textSize.Width) / 2f, 0f),
            Math.Max((height - textSize.Height) / 2f, 0f));
        var renderer = new UIGraphicsImageRenderer(imageSize);
        var image = renderer.CreateImage(_ => glyphText.DrawString(drawPoint, attributes));
        return image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysOriginal);
    }

    private static UIFont? ResolvePlatformFont(string fontFamily, nfloat fontSize)
    {
        var font = UIFont.FromName(fontFamily, fontSize);
        if (font is not null)
        {
            return font;
        }

        var bundlePath = NSBundle.MainBundle.PathForResource(fontFamily, "ttf");
        if (bundlePath is not null)
        {
            var url = NSUrl.FromFilename(bundlePath);
            CTFontManager.RegisterFontsForUrl(url, CTFontManagerScope.Process);
            font = UIFont.FromName(fontFamily, fontSize);
        }

        return font;
    }

    private static UIColor ResolveBackgroundColor(ToggleButton view)
    {
        return ResolveBrushColor(view.Background as SolidColorBrush, UIColor.Clear);
    }

    private static UIColor ResolveBorderColor(ToggleButton view, UIColor fallbackColor)
    {
        var defaultBorderColor = view.IsChecked ? fallbackColor.ColorWithAlpha(0.75f) : fallbackColor.ColorWithAlpha(0.35f);
        return ResolveBrushColor(view.BorderBrush as SolidColorBrush, defaultBorderColor);
    }

    // Content (glyph/text) color: normal color when unchecked, accent color when checked.
    private static UIColor ResolveContentColor(ToggleButton view)
    {
        return view.IsChecked ? ResolveAccentColor(view) : ResolveNormalColor(view);
    }

    // Adaptive foreground used in the unselected state (dark text on light theme, light text on dark theme).
    // The value is resolved eagerly against the theme MAUI is actually applying: the glyph is baked
    // into a bitmap, and the ambient trait collection at render time is not necessarily the window's
    // (it still reports the OS style while UserAppTheme overrides it), which left glyphs white in light mode.
    private static UIColor ResolveNormalColor(ToggleButton view)
    {
        _ = view;
        return UIColor.Label.GetResolvedColor(ResolveTraitCollection());
    }

    private static UITraitCollection ResolveTraitCollection()
    {
        var style = Application.Current?.RequestedTheme == AppTheme.Dark
            ? UIUserInterfaceStyle.Dark
            : UIUserInterfaceStyle.Light;
        return UITraitCollection.FromUserInterfaceStyle(style);
    }

    // Accent color used for the selected/checked state and the highlight background.
    private static UIColor ResolveAccentColor(ToggleButton view)
    {
        return ResolveBrushColor(view.Foreground, global::Microsoft.Maui.Graphics.Colors.DodgerBlue.ToPlatform());
    }

    private static UIColor ResolveBrushColor(SolidColorBrush? brush, UIColor fallbackColor)
    {
        var color = brush?.Color;
        return color is null ? fallbackColor : color.ToPlatform();
    }
}
