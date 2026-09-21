using CoreGraphics;
using CoreText;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleDropdownHandler : ViewHandler<ToggleDropdown, UIButton>
{
    public static PropertyMapper<ToggleDropdown, ToggleDropdownHandler> PropertyMapper = new(ViewMapper)
    {
        ["UseSystemFocusVisuals"] = (handler, view) => FocusRing.UpdateSystemControl(handler.PlatformView, view),
        [nameof(ToggleDropdown.Padding)] = MapPadding,
        [nameof(ToggleDropdown.UnselectedText)] = MapText,
        [nameof(ToggleDropdown.FontSize)] = MapFontSize,
        [nameof(ToggleDropdown.Spacing)] = MapSpacing,
        [nameof(ToggleDropdown.Orientation)] = MapOrientation,
        [nameof(ToggleDropdown.HorizontalContentAlignment)] = MapHorizontalContentAlignment,
        [nameof(ToggleDropdown.BorderThickness)] = MapBorderThickness,
        [nameof(ToggleDropdown.IconFontSize)] = MapIconFontSize,
        [nameof(ToggleDropdown.IconFontWeight)] = MapIconFontWeight,
        [nameof(ToggleDropdown.Options)] = VirtualView_Options_CollectionChanged,
        [nameof(ToggleDropdown.SelectedOption)] = MapSelectedOption,
        [nameof(ToggleDropdown.IsChecked)] = MapIsSelected,
        [nameof(ToggleDropdown.TintColor)] = MapColor,
        [nameof(ToggleDropdown.TextColor)] = MapColor,
        [nameof(ToggleDropdown.BorderColor)] = MapColor,
        [nameof(ToggleDropdown.CornerRadius)] = MapColor,
        [nameof(ToggleDropdown.DropdownIndicatorGlyph)] = MapColor,
        [nameof(ToggleDropdown.DropdownIndicatorFontFamily)] = MapColor,
        [nameof(ToggleDropdown.ChangeUnselectedTextOnSelectionChange)] = MapText,
        [nameof(ToggleDropdown.IsActionMenu)] = MapIsActionMenu,
        [nameof(ToggleDropdown.IconGlyph)] = MapIcon,
        [nameof(ToggleDropdown.IconFontFamily)] = MapIcon,
        [nameof(ToggleDropdown.SystemIconName)] = MapIcon,
        [nameof(IView.IsEnabled)] = MapIsEnabled,
        // VoiceOver (see UpdateAccessibility); MAUI's own semantics mapping would clear the label.
        [nameof(IView.Semantics)] = (handler, view) =>
        {
            ViewHandler.MapSemantics(handler, view);
            UpdateAccessibility(handler.PlatformView, view);
        },
        ["ToolTip"] = (handler, view) =>
        {
            ViewHandler.MapToolTip(handler, view);
            UpdateAccessibility(handler.PlatformView, view);
        },
    };

    public static new void MapIsEnabled(IViewHandler handler, IView view)
    {
        ViewHandler.MapIsEnabled(handler, view);
        if (handler is ToggleDropdownHandler { PlatformView: { } button } && view is ToggleDropdown dropdown)
        {
            UpdateButtonAppearance(button, dropdown);
        }
    }

    private PointerHoverTracker? _hover;
    private UIImageView? _indicatorView;

    public ToggleDropdownHandler() : base(PropertyMapper)
    {
    }

    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        ConfigureButton(platformView);
        // The primary action: a click, VoiceOver's activate, or Space/Return with keyboard focus.
        platformView.AddTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);

        // UWP ToggleDropdown shows PointerOver/Pressed backgrounds; UIKit reports hover through a
        // hover recognizer and pressed through the button's Highlighted state.
        _hover = new PointerHoverTracker(platformView, () => PlatformView?.SetNeedsUpdateConfiguration());
        platformView.ConfigurationUpdateHandler = ApplyInteractionState;

        _indicatorView = new UIImageView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            ContentMode = UIViewContentMode.Center,
            UserInteractionEnabled = false,
            Hidden = true,
        };
        platformView.AddSubview(_indicatorView);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _indicatorView.TrailingAnchor.ConstraintEqualTo(platformView.TrailingAnchor, -1),
            _indicatorView.CenterYAnchor.ConstraintEqualTo(platformView.CenterYAnchor),
        });

        UpdateButtonAppearance(platformView, VirtualView);
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        platformView.RemoveTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);
        _hover?.Dispose();
        _hover = null;

        platformView.ConfigurationUpdateHandler = null;
        _indicatorView?.RemoveFromSuperview();
        _indicatorView = null;
        base.DisconnectHandler(platformView);
    }

    // The template's states: PointerOver and Pressed paint the root with ButtonBackgroundPointerOver
    // and ButtonBackgroundPressed, checked or not (the checked look is the accent foreground), and
    // Disabled keeps the PointerOver fill under 60% opacity.
    private void ApplyInteractionState(UIButton button)
    {
        var configuration = button.Configuration;
        if (configuration is null)
        {
            return;
        }

        var background = configuration.Background;
        background.BackgroundColor = !button.Enabled
            ? WithOpacity(InteractionColors.PointerOver(), 0.6f)
            : button.Highlighted
                ? InteractionColors.Pressed()
                : _hover?.IsHovered == true ? InteractionColors.PointerOver() : UIColor.Clear;
        configuration.Background = background;
        button.Configuration = configuration;

        static UIColor WithOpacity(UIColor color, float opacity) => color.ColorWithAlpha(color.CGColor.Alpha * opacity);
    }

    private void ConfigureButton(UIButton button)
    {
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        button.ClipsToBounds = true;
        button.ChangesSelectionAsPrimaryAction = false;
        button.ShowsMenuAsPrimaryAction = false;
    }

    // UWP: a click checks an unchecked dropdown; clicking it once checked opens its options (an
    // action menu opens them on every click).
    private void ButtonTapped(object? sender, EventArgs e)
    {
        if (!VirtualView.IsActionMenu && !VirtualView.IsChecked)
        {
            VirtualView.MarkNextToggleAsUserInitiated();
            VirtualView.IsChecked = true;
            return;
        }

        if (VirtualView.Options is { Count: > 0 } options && (VirtualView.IsActionMenu || options.Count > 1))
        {
            ShowOptions(options);
        }
    }

    private void ShowOptions(IList<Controls.CustomObjects.SelectorOption> options)
    {
        var selected = VirtualView.IsActionMenu || !VirtualView.SelectedOption.HasValue
            ? -1
            : options.IndexOf(VirtualView.SelectedOption.Value);
        var items = options
            .Select((option, index) => new DropdownMenuItem(option.Text, CreateOptionImage(option), index == selected))
            .ToList();
        DropdownOptionsMenu.Show(PlatformView, items, VirtualView.TintColor.ToPlatform(), OptionChosen);
    }

    private void OptionChosen(int index)
    {
        if (VirtualView is null || index < 0 || index >= VirtualView.Options.Count)
        {
            return;
        }

        var selectedOption = VirtualView.Options[index];

        VirtualView.MarkNextSelectionAsUserInitiated();
        if (!VirtualView.IsActionMenu)
        {
            VirtualView.MarkNextToggleAsUserInitiated();
            VirtualView.IsChecked = true;
        }
        VirtualView.SelectedOption = selectedOption;

        if (VirtualView.IsActionMenu)
        {
            VirtualView.SetSelectedOptionFromCompatibility(null, true);
        }
    }

    // MAUI measures a UIButton that has an image but no CurrentTitle as its image alone. The label
    // lives in the button's configuration, so CurrentTitle is always null and every dropdown was
    // sized to its bare icon (16x16), which squeezed DrawingItemsView's compact grid. Ask the button
    // for its real size instead, keeping MAUI's explicit and minimum/maximum sizes.
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        IView view = VirtualView;
        var width = IsExplicit(view.Width) ? view.Width : Math.Min(widthConstraint, view.MaximumWidth);
        var height = IsExplicit(view.Height) ? view.Height : Math.Min(heightConstraint, view.MaximumHeight);
        var fits = PlatformView.SizeThatFits(new CGSize(
            double.IsFinite(width) ? width : nfloat.PositiveInfinity,
            double.IsFinite(height) ? height : nfloat.PositiveInfinity));

        return new Size(
            Resolve((double)fits.Width, view.Width, view.MinimumWidth, view.MaximumWidth),
            Resolve((double)fits.Height, view.Height, view.MinimumHeight, view.MaximumHeight));

        static double Resolve(double measured, double explicitSize, double minimum, double maximum)
        {
            var size = IsExplicit(explicitSize) ? explicitSize : measured;
            size = Math.Min(size, maximum);
            return minimum > 0 ? Math.Max(size, minimum) : size;
        }

        // MAUI marks an unset WidthRequest/HeightRequest as -1.
        static bool IsExplicit(double value) => value >= 0 && double.IsFinite(value);
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new ToggleDropdownButton
        {
            ChangesSelectionAsPrimaryAction = true
        };
        MacIdiomControlStyle.Apply(button);
        return button;
    }

    public static void MapText(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    // UIButton keeps its own subviews ordered; reposition the indicator after every appearance change.
    private void UpdateIndicator(UIButton button, ToggleDropdown view, UIColor foreground)
    {
        if (_indicatorView is null)
        {
            return;
        }

        var hasMenu = view.Options is { Count: > 1 } && !view.IsActionMenu;
        _indicatorView.Hidden = !(hasMenu && view.IsChecked);
        if (_indicatorView.Hidden)
        {
            return;
        }

        UIImage? image = null;
        if (!string.IsNullOrWhiteSpace(view.DropdownIndicatorGlyph) && !string.IsNullOrWhiteSpace(view.DropdownIndicatorFontFamily))
        {
            image = CreateFontGlyphImage(view.DropdownIndicatorGlyph, view.DropdownIndicatorFontFamily, 10, FontAttributes.None);
        }

        image ??= UIImage.GetSystemImage("chevron.down", UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(7, UIFontWeight.Semibold)));
        _indicatorView.Image = image?.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
        _indicatorView.TintColor = foreground;
        button.BringSubviewToFront(_indicatorView);
    }

    public static void MapPadding(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapFontSize(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapSpacing(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapOrientation(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapHorizontalContentAlignment(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapBorderThickness(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapIconFontSize(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapIconFontWeight(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapSelectedOption(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapIsSelected(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapColor(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapIsActionMenu(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapIcon(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    private static void VirtualView_Options_CollectionChanged(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    // VoiceOver: a toggle (an action menu is a plain button) named by its text or, icon-only, its
    // tooltip; the chosen option is its value when the name does not already say it.
    private static void UpdateAccessibility(UIButton button, ToggleDropdown view)
    {
        var title = button.Configuration?.Title;
        ButtonAccessibility.Update(button, view, isToggle: !view.IsActionMenu, isOn: !view.IsActionMenu && view.IsChecked, text: title);
        if (!view.IsActionMenu && view.SelectedOption?.Text is { Length: > 0 } option && option != button.AccessibilityLabel)
        {
            button.AccessibilityValue = option;
        }
    }

    private static void UpdateButtonAppearance(UIButton button, ToggleDropdown view)
    {
        var visuallySelected = !view.IsActionMenu && view.IsChecked;

        // UWP template: theme foreground normally, the accent only in the Checked states. Dynamic
        // UIColors re-resolve on appearance changes, and template-rendered glyphs pick them up.
        var foreground = visuallySelected
            ? view.TintColor.ToPlatform()
            : view.TextColor?.ToPlatform() ?? UIColor.Label;
        if (!view.IsEnabled)
        {
            // UWP Disabled state renders the whole control at 60% opacity.
            foreground = foreground.ColorWithAlpha(0.6f);
        }

        var configuration = button.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
        var title = view.IsActionMenu
            ? view.UnselectedText
            : view.ChangeUnselectedTextOnSelectionChange ? (view.SelectedOption?.Text ?? view.UnselectedText) : view.UnselectedText;
        title ??= string.Empty;
        var image = CreateSelectedImage(view);

        configuration.Title = string.IsNullOrEmpty(title) ? null : title;
        configuration.Image = image;
        configuration.ImagePlacement = view.Orientation == StackOrientation.Vertical
            ? NSDirectionalRectEdge.Top
            : NSDirectionalRectEdge.Leading;
        configuration.ImagePadding = image is null ? 0 : (nfloat)Math.Max(0, view.Spacing);
        configuration.BaseForegroundColor = foreground;
        configuration.ContentInsets = ResolveContentInsets(view.Padding);
        configuration.TitleLineBreakMode = view.Orientation == StackOrientation.Vertical
            ? UILineBreakMode.WordWrap
            : UILineBreakMode.TailTruncation;
        configuration.TitleAlignment = UIButtonConfigurationTitleAlignment.Center;

        // Fixed: the configuration's default (dynamic) corner style replaces background.CornerRadius
        // with a system radius of its own.
        configuration.CornerStyle = UIButtonConfigurationCornerStyle.Fixed;
        var background = configuration.Background;
        background.CornerRadius = (nfloat)Math.Max(0, view.CornerRadius);
        background.StrokeWidth = (nfloat)Math.Max(0, view.BorderThickness);
        background.StrokeColor = view.BorderColor?.ToPlatform() ?? UIColor.Separator;
        configuration.Background = background;

        // A UIButtonConfiguration reasserts its own title font on every layout pass,
        // overriding any direct titleLabel.Font assignment. Enforce the requested
        // FontSize through the configuration's attribute transformer so it takes effect.
        var titleFont = GetSystemFont((nfloat)Math.Max(view.FontSize, 8d));
        configuration.TitleTextAttributesTransformer = incoming =>
        {
            // 'incoming' is an immutable (frozen) dictionary; mutate a copy instead.
            var attributes = new UIStringAttributes(NSMutableDictionary.FromDictionary(incoming))
            {
                Font = titleFont,
            };
            return attributes.Dictionary;
        };

        button.Configuration = configuration;
        if (button is ToggleDropdownButton measuredButton)
        {
            measuredButton.TitleFont = titleFont;
            measuredButton.MaxTitleLines = view.Orientation == StackOrientation.Vertical ? 2 : 1;
        }

        // Text, icon, font or padding may have changed the size the button needs.
        ((IView)view).InvalidateMeasure();
        button.HorizontalAlignment = ResolveContentHorizontalAlignment(view.HorizontalContentAlignment);
        var titleLabel = button.TitleLabel;
        if (titleLabel is not null)
        {
            titleLabel.Lines = view.Orientation == StackOrientation.Vertical ? 2 : 1;
        }

        button.Layer.CornerRadius = (nfloat)Math.Max(0, view.CornerRadius);
        button.Selected = visuallySelected;
        (view.Handler as ToggleDropdownHandler)?.UpdateIndicator(button, view, foreground);
        UpdateAccessibility(button, view);
        button.SetNeedsUpdateConfiguration();
        button.SetNeedsLayout();
    }

    private static NSDirectionalEdgeInsets ResolveContentInsets(Thickness padding)
    {
        return new NSDirectionalEdgeInsets(
            (nfloat)Math.Max(0, padding.Top),
            (nfloat)Math.Max(0, padding.Left),
            (nfloat)Math.Max(0, padding.Bottom),
            (nfloat)Math.Max(0, padding.Right));
    }

    private static UIImage? CreateSelectedImage(ToggleDropdown view)
    {
        if (!string.IsNullOrWhiteSpace(view.SystemIconName))
        {
            return CreateSystemImage(view.SystemIconName);
        }

        if (!string.IsNullOrWhiteSpace(view.IconGlyph) && !string.IsNullOrWhiteSpace(view.IconFontFamily))
        {
            return CreateFontGlyphImage(
                view.IconGlyph,
                view.IconFontFamily,
                view.IconFontSize,
                view.IconFontWeight);
        }

        if (!view.SelectedOption.HasValue)
        {
            return null;
        }

        var option = view.SelectedOption.Value;
        if (!string.IsNullOrWhiteSpace(option.SystemIconName))
        {
            return CreateSystemImage(option.SystemIconName);
        }

        if (string.IsNullOrWhiteSpace(option.IconGlyph) || string.IsNullOrWhiteSpace(option.IconFontFamily))
        {
            return null;
        }

        var iconFontSize = view.IconFontSize > 0 ? view.IconFontSize : option.IconFontSize;
        return CreateFontGlyphImage(option.IconGlyph, option.IconFontFamily, iconFontSize, view.IconFontWeight);
    }

    private static UIImage? CreateOptionImage(Controls.CustomObjects.SelectorOption? selectedOption)
    {
        if (!selectedOption.HasValue)
        {
            return null;
        }

        var option = selectedOption.Value;
        if (!string.IsNullOrWhiteSpace(option.SystemIconName))
        {
            return CreateSystemImage(option.SystemIconName);
        }

        if (string.IsNullOrWhiteSpace(option.IconGlyph) || string.IsNullOrWhiteSpace(option.IconFontFamily))
        {
            return null;
        }

        return CreateFontGlyphImage(option.IconGlyph, option.IconFontFamily, option.IconFontSize, FontAttributes.None);
    }

    private static UIImage? CreateSystemImage(string systemIconName)
    {
        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        return UIImage.GetSystemImage(systemIconName, config)?.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
    }

    // Glyphs are rendered once as template images: the button and menu tint them with the current
    // (dynamic) foreground, so checked/unchecked and light/dark need no re-rendering.
    private static UIImage? CreateFontGlyphImage(string glyph, string fontFamily, double fontSize, FontAttributes fontAttributes)
    {
        var resolvedFontSize = (nfloat)Math.Max(fontSize > 0 ? fontSize : 16d, 6d);
        var font = ResolvePlatformFont(fontFamily, resolvedFontSize) ?? UIFont.SystemFontOfSize(resolvedFontSize);
        font = ApplyFontAttributes(font, resolvedFontSize, fontAttributes);
        var attributes = new UIStringAttributes
        {
            Font = font,
            ForegroundColor = UIColor.Black,
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
        return image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
    }

    private static UIFont ApplyFontAttributes(UIFont? font, nfloat fontSize, FontAttributes fontAttributes)
    {
        var resolvedFont = font ?? GetSystemFont(fontSize)!;

        if ((fontAttributes & FontAttributes.Bold) != 0)
        {
            var baseDescriptor = resolvedFont!.FontDescriptor;
            if (baseDescriptor is not null)
            {
                var boldDescriptor = baseDescriptor.CreateWithTraits(baseDescriptor.SymbolicTraits | UIFontDescriptorSymbolicTraits.Bold);
                if (boldDescriptor is not null)
                {
                    var boldFont = UIFont.FromDescriptor(boldDescriptor, fontSize);
                    if (boldFont is not null)
                    {
                        resolvedFont = boldFont;
                    }
                }
            }
        }

        if ((fontAttributes & FontAttributes.Italic) != 0)
        {
            var baseDescriptor = resolvedFont!.FontDescriptor;
            if (baseDescriptor is not null)
            {
                var italicDescriptor = baseDescriptor.CreateWithTraits(baseDescriptor.SymbolicTraits | UIFontDescriptorSymbolicTraits.Italic);
                if (italicDescriptor is not null)
                {
                    var italicFont = UIFont.FromDescriptor(italicDescriptor, fontSize);
                    if (italicFont is not null)
                    {
                        resolvedFont = italicFont;
                    }
                }
            }
        }

        return resolvedFont!;
    }

    private static UIFont GetSystemFont(nfloat fontSize)
    {
        return UIFont.SystemFontOfSize(fontSize)
            ?? throw new InvalidOperationException("Unable to resolve the system font.");
    }

    internal static UIFont? ResolvePlatformFont(string fontFamily, nfloat fontSize)
    {
        var font = UIFont.FromName(fontFamily, fontSize);
        if (font is not null)
        {
            return font;
        }

        // MAUI-registered fonts may not yet be available via UIFont.FromName.
        // Manually register the font from the app bundle as a fallback.
        var bundlePath = NSBundle.MainBundle.PathForResource(fontFamily, "ttf");
        if (bundlePath is not null)
        {
            var url = NSUrl.FromFilename(bundlePath);
            CTFontManager.RegisterFontsForUrl(url, CTFontManagerScope.Process);
            font = UIFont.FromName(fontFamily, fontSize);
        }

        return font;
    }

    public static UIControlContentHorizontalAlignment ResolveContentHorizontalAlignment(LayoutOptions alignment)
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

}

/// <summary>
/// The dropdown's button, measured from what its configuration draws: padding, icon, gap and a title
/// wrapped at the available width (up to two lines when the icon sits on top), in the configured
/// title font, which UIButton's own sizeThatFits does not account for.
/// </summary>
/// <remarks>
/// Created with the default constructor: UIButton(UIButtonType) makes UIKit build a plain UIButton
/// (buttonWithType: on the base class), so this subclass's overrides never ran natively and the
/// binding logged a stack trace per button. The button type does not matter here: the look comes
/// entirely from the UIButtonConfiguration the handler sets.
/// </remarks>
internal sealed class ToggleDropdownButton : UIButton
{
    public ToggleDropdownButton()
    {
    }

    public UIFont? TitleFont { get; set; }

    public int MaxTitleLines { get; set; } = 1;

    public override CGSize SizeThatFits(CGSize size)
    {
        if (Configuration is not { } configuration || TitleFont is not { } font)
        {
            return base.SizeThatFits(size);
        }

        var insets = configuration.ContentInsets;
        var horizontalPadding = (double)(insets.Leading + insets.Trailing);
        var verticalPadding = (double)(insets.Top + insets.Bottom);
        var image = configuration.Image?.Size ?? CGSize.Empty;
        var title = configuration.Title;
        var hasTitle = !string.IsNullOrEmpty(title);
        var gap = image.Width > 0 && hasTitle ? (double)configuration.ImagePadding : 0;
        var vertical = configuration.ImagePlacement == NSDirectionalRectEdge.Top;

        var available = double.IsFinite((double)size.Width) && size.Width > 0 && size.Width < 100_000
            ? (double)size.Width - horizontalPadding
            : double.PositiveInfinity;
        var titleWidthLimit = vertical ? available : available - image.Width - gap;
        var text = hasTitle ? MeasureTitle(title!, font, titleWidthLimit, vertical ? MaxTitleLines : 1) : CGSize.Empty;

        double width, height;
        if (vertical)
        {
            width = Math.Max((double)image.Width, (double)text.Width) + horizontalPadding;
            height = verticalPadding + (double)image.Height + gap + (double)text.Height;
        }
        else
        {
            width = horizontalPadding + (double)image.Width + gap + (double)text.Width;
            height = verticalPadding + Math.Max((double)image.Height, (double)text.Height);
        }

        return new CGSize(Math.Ceiling(width), Math.Ceiling(height));
    }

    public override CGSize IntrinsicContentSize => SizeThatFits(new CGSize(nfloat.PositiveInfinity, nfloat.PositiveInfinity));

    private static CGSize MeasureTitle(string title, UIFont font, double widthLimit, int maxLines)
    {
        var lineHeight = (double)font.LineHeight;
        var bounds = new CGSize(
            double.IsFinite(widthLimit) ? Math.Max(1, widthLimit) : 100_000,
            lineHeight * Math.Max(1, maxLines) + 1);
        using var text = new NSString(title);
        var rect = text.GetBoundingRect(bounds, NSStringDrawingOptions.UsesLineFragmentOrigin, new UIStringAttributes { Font = font }, null);
        return new CGSize(
            Math.Min(Math.Ceiling((double)rect.Width), bounds.Width),
            Math.Min(Math.Ceiling((double)rect.Height), lineHeight * Math.Max(1, maxLines)));
    }
}
