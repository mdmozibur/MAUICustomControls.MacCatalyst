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
    };

    public ToggleDropdownHandler() : base(PropertyMapper)
    {
    }

    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        ConfigureButton(platformView);
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);

        UpdateMenu(platformView, VirtualView, OptionChosen);
        UpdateButtonAppearance(platformView, VirtualView);
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        platformView.RemoveTarget(ButtonTapped, UIControlEvent.TouchUpInside);
        base.DisconnectHandler(platformView);
    }

    private void ConfigureButton(UIButton button)
    {
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        button.ClipsToBounds = true;
        button.Layer.CornerRadius = 12;
        button.TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
        button.ChangesSelectionAsPrimaryAction = false;
        button.ShowsMenuAsPrimaryAction = false;
    }

    private void ButtonTapped(object? sender, EventArgs e)
    {
        if (!VirtualView.IsChecked)
        {
            VirtualView.MarkNextToggleAsUserInitiated();
            VirtualView.IsChecked = true;
        }
    }

    private void OptionChosen(UIAction action)
    {
        var selectedOption = VirtualView.Options.FirstOrDefault(o => string.Equals(o.Text, action.Title, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(selectedOption.Text))
        {
            return;
        }

        VirtualView.MarkNextToggleAsUserInitiated();
        VirtualView.MarkNextSelectionAsUserInitiated();
        VirtualView.IsChecked = true;
        VirtualView.SelectedOption = selectedOption;
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton(UIButtonType.System)
        {
            ChangesSelectionAsPrimaryAction = true
        };
        return button;
    }

    public static void MapText(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
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
        UpdateMenu(handler.PlatformView, view, handler.OptionChosen);
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

    private static void VirtualView_Options_CollectionChanged(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        UpdateMenu(handler.PlatformView, view, handler.OptionChosen);
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    private static void UpdateMenu(UIButton button, ToggleDropdown view, UIActionHandler optionChosen)
    {
        if (view.Options is null || view.Options.Count == 0)
        {
            button.Menu = null;
            return;
        }

        var selectedText = view.SelectedOption?.Text;
        var tintColor = view.TintColor.ToPlatform();
        var menuItems = new UIAction[view.Options.Count];

        for (int i = 0; i < view.Options.Count; i++)
        {
            var option = view.Options[i];
            var image = CreateOptionImage(option, tintColor);
            var action = UIAction.Create(option.Text, image, null, optionChosen);
            action.State = string.Equals(option.Text, selectedText, StringComparison.Ordinal) ? UIMenuElementState.On : UIMenuElementState.Off;
            menuItems[i] = action;
        }

        button.Menu = UIMenu.Create(menuItems);
    }

    private static void UpdateButtonAppearance(UIButton button, ToggleDropdown view)
    {
        var tintColor = view.TintColor.ToPlatform();
        var configuration = button.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
        var title = view.ChangeUnselectedTextOnSelectionChange ? (view.SelectedOption?.Text ?? view.UnselectedText) : view.UnselectedText;
        title ??= string.Empty;
        var image = CreateSelectedImage(view, tintColor);

        configuration.Title = title;
        configuration.Image = image;
        configuration.ImagePlacement = view.Orientation == StackOrientation.Vertical
            ? NSDirectionalRectEdge.Top
            : NSDirectionalRectEdge.Leading;
        configuration.ImagePadding = image is null ? 0 : (nfloat)Math.Max(0, view.Spacing);
        configuration.BaseForegroundColor = tintColor;
        configuration.ContentInsets = ResolveContentInsets(view.Padding);
        configuration.Background.CornerRadius = 0;

        button.Configuration = configuration;
        button.SetTitle(title, UIControlState.Normal);
        button.SetTitle(title, UIControlState.Selected);
        button.SetTitleColor(tintColor, UIControlState.Normal);
        button.SetTitleColor(tintColor, UIControlState.Selected);
        button.SetImage(image, UIControlState.Normal);
        button.SetImage(image, UIControlState.Selected);
        button.TintColor = tintColor;
        button.HorizontalAlignment = ResolveContentHorizontalAlignment(view.HorizontalContentAlignment);
        var titleLabel = button.TitleLabel;
        if (titleLabel is not null)
        {
            titleLabel.Font = GetSystemFont((nfloat)Math.Max(view.FontSize, 8d))!;
            titleLabel.Lines = view.Orientation == StackOrientation.Vertical ? 2 : 1;
            titleLabel.LineBreakMode = view.Orientation == StackOrientation.Vertical
                ? UILineBreakMode.WordWrap
                : UILineBreakMode.TailTruncation;
            titleLabel.TextAlignment = UITextAlignment.Center;
        }
        button.Selected = view.IsChecked;
        button.ShowsMenuAsPrimaryAction = view.IsChecked && button.Menu is not null;
        button.Layer.BorderWidth = (float)view.BorderThickness;
        button.Layer.BorderColor = tintColor.ColorWithAlpha(view.IsChecked ? 0.75f : 0.35f).CGColor;
        button.BackgroundColor = view.IsChecked ? tintColor.ColorWithAlpha(0.14f) : UIColor.Clear;
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

    private static UIImage? CreateSelectedImage(ToggleDropdown view, UIColor tintColor)
    {
        if (!view.SelectedOption.HasValue)
        {
            return null;
        }

        var option = view.SelectedOption.Value;
        if (!string.IsNullOrWhiteSpace(option.SystemIconName))
        {
            var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
            var image = UIImage.GetSystemImage(option.SystemIconName, config);
            return image?.ApplyTintColor(tintColor, UIImageRenderingMode.AlwaysOriginal);
        }

        if (string.IsNullOrWhiteSpace(option.IconGlyph) || string.IsNullOrWhiteSpace(option.IconFontFamily))
        {
            return null;
        }

        var iconFontSize = view.IconFontSize > 0 ? view.IconFontSize : option.IconFontSize;
        return CreateFontGlyphImage(option.IconGlyph, option.IconFontFamily, iconFontSize, tintColor, view.IconFontWeight);
    }

    private static UIImage? CreateOptionImage(Controls.CustomObjects.SelectorOption? selectedOption, UIColor tintColor)
    {
        if (!selectedOption.HasValue)
        {
            return null;
        }

        var option = selectedOption.Value;
        if (!string.IsNullOrWhiteSpace(option.SystemIconName))
        {
            var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
            var image = UIImage.GetSystemImage(option.SystemIconName, config);
            return image?.ApplyTintColor(tintColor, UIImageRenderingMode.AlwaysOriginal);
        }

        if (string.IsNullOrWhiteSpace(option.IconGlyph) || string.IsNullOrWhiteSpace(option.IconFontFamily))
        {
            return null;
        }

        return CreateFontGlyphImage(option.IconGlyph, option.IconFontFamily, option.IconFontSize, tintColor, FontAttributes.None);
    }

    private static UIImage? CreateFontGlyphImage(string glyph, string fontFamily, double fontSize, UIColor tintColor, FontAttributes fontAttributes)
    {
        var resolvedFontSize = (nfloat)Math.Max(fontSize > 0 ? fontSize : 16d, 8d);
        var font = ResolvePlatformFont(fontFamily, resolvedFontSize) ?? UIFont.SystemFontOfSize(resolvedFontSize);
        font = ApplyFontAttributes(font, resolvedFontSize, fontAttributes);
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

    private static UIFont? ResolvePlatformFont(string fontFamily, nfloat fontSize)
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