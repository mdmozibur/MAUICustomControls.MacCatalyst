

using Microsoft.Maui.Handlers;
using UIKit;
using Microsoft.Maui.Platform;
using CheckBox = MAUICustomControls.MacCatalyst.Controls.CheckBox;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class CheckBoxHandler : ViewHandler<CheckBox, UIButton>
{
    public static PropertyMapper<CheckBox, CheckBoxHandler> PropertyMapper = new(ViewMapper)
    {
        ["UseSystemFocusVisuals"] = (handler, view) => FocusRing.UpdateSystemControl(handler.PlatformView, view),
        [nameof(CheckBox.Text)] = MapText,
        [nameof(CheckBox.FontSize)] = MapFontSize,
        [nameof(CheckBox.FontAttributes)] = MapFontAttributes,
        [nameof(CheckBox.BorderThickness)] = MapBorderThickness,
        [nameof(CheckBox.IsChecked)] = MapIsSelected,
        [nameof(CheckBox.Foreground)] = MapColor,
        // VoiceOver: a toggle with its checked state; MAUI's semantics mapping would clear the label.
        [nameof(IView.Semantics)] = (handler, view) =>
        {
            ViewHandler.MapSemantics(handler, view);
            UpdateAccessibility(handler, view);
        },
        [nameof(IView.IsEnabled)] = (handler, view) =>
        {
            ViewHandler.MapIsEnabled(handler, view);
            UpdateAccessibility(handler, view);
        },
    };

    // WinUI fills a checked box with the accent colour and draws the check on it; an unchecked box
    // is an outline in the strong stroke colour. The filled symbol takes the button's tint (the
    // window tint, which the app sets to the system accent); the outline is drawn as the caption.
    static UIImage CheckedImage = UIImage.GetSystemImage("checkmark.square.fill");
    static UIImage UncheckedImage = UIImage.GetSystemImage("square");
    static UIImage BoxFillImage = UIImage.GetSystemImage("square.fill");

    // WinUI's PointerOver state: the unchecked box gets a faint fill
    // (CheckBoxCheckBackgroundFillUncheckedPointerOver = ControlAltFillColorTertiary), the checked
    // box turns AccentFillColorSecondary (the accent at 90%).
    private PointerHoverTracker? _hover;

    public CheckBoxHandler() : base(PropertyMapper)
    {
    }

    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        // Deliberately no UIButtonConfiguration. A configuration recomputes the title's font and
        // colour from itself on every update, which silently discarded everything MapFontSize and
        // MapColor set through TitleLabel - the label rendered at UIKit's default body size
        // instead of the size the style asked for. The legacy title path honours both.
        platformView.Layer.BorderWidth = 0;
        platformView.Layer.CornerRadius = 0;
        platformView.BackgroundColor = UIColor.Clear;

        // The legacy layout butts the title straight up against the box glyph. Nudge it across
        // and give the content the width back, which is what the configuration's imagePadding
        // used to do.
        const int glyphGap = 6;
        platformView.TitleEdgeInsets = new UIEdgeInsets(0, glyphGap, 0, -glyphGap);
        platformView.ContentEdgeInsets = new UIEdgeInsets(0, 0, 0, glyphGap);
        platformView.HorizontalAlignment = ToggleDropdownHandler.ResolveContentHorizontalAlignment(VirtualView.HorizontalContentAlignment);
        
        // The primary action, not TouchUpInside: a click, VoiceOver's activate and Space/Return on
        // the focused box (keyboard navigation) all send it, exactly once.
        platformView.AddTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);
        _hover = new PointerHoverTracker(platformView, () =>
        {
            if (VirtualView is { } view)
            {
                MapIsSelected(this, view);
            }
        });

        MapText(this, VirtualView);
        MapIsSelected(this, VirtualView);
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        platformView.RemoveTarget(ButtonTapped, UIControlEvent.PrimaryActionTriggered);
        _hover?.Dispose();
        _hover = null;
        base.DisconnectHandler(platformView);
    }

    private void ButtonTapped(object? sender, EventArgs e)
    {
        VirtualView.IsChecked = !VirtualView.IsChecked;
        VirtualView.RaiseClicked();
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton(UIButtonType.System);
        MacIdiomControlStyle.Apply(button);
        return button;
    }

    public static void MapText(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.SetTitle(view.Text, UIControlState.Normal);
        UpdateAccessibility(handler, view);
    }

    private static void UpdateAccessibility(CheckBoxHandler handler, CheckBox view) =>
        ButtonAccessibility.Update(handler.PlatformView, view, isToggle: true, isOn: view.IsChecked, text: view.Text);

    public static void MapFontSize(CheckBoxHandler handler, CheckBox view) => ApplyFont(handler, view);

    public static void MapFontAttributes(CheckBoxHandler handler, CheckBox view) => ApplyFont(handler, view);

    private static void ApplyFont(CheckBoxHandler handler, CheckBox view)
    {
        var label = handler.PlatformView.TitleLabel;
        if (label is null)
        {
            return;
        }

        var size = (nfloat)view.FontSize;
        label.Font = view.FontAttributes switch
        {
            FontAttributes.Bold => UIFont.BoldSystemFontOfSize(size),
            FontAttributes.Italic => UIFont.ItalicSystemFontOfSize(size),
            _ => UIFont.SystemFontOfSize(size),
        };
    }

    public static void MapBorderThickness(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.Layer.BorderWidth = (float)view.BorderThickness;
        handler.PlatformView.Layer.CornerRadius = 0;
    }

    public static void MapIsSelected(CheckBoxHandler handler, CheckBox view)
    {
        var platformView = handler.PlatformView;
        var hovered = handler._hover?.IsHovered == true && view.IsEnabled;
        platformView.SetImage(
            view.IsChecked ? CheckedImage : hovered ? HoveredUncheckedImage(view) : UncheckedImage,
            UIControlState.Normal);
        ApplyGlyphTint(handler, view);
        if (view.IsChecked && hovered)
        {
            platformView.TintColor = InteractionColors.Get(
                "CheckBoxCheckBackgroundFillCheckedPointerOver",
                platformView.TintColor.ColorWithAlpha(platformView.TintColor.CGColor.Alpha * 0.9f));
        }
        UpdateAccessibility(handler, view);
    }
    
    // The unchecked outline over WinUI's faint pointer-over fill, drawn as one image in its final
    // colours (a template image would take a single tint).
    private static UIImage HoveredUncheckedImage(CheckBox view)
    {
        var fill = InteractionColors.Get("CheckBoxCheckBackgroundFillUncheckedPointerOver", InteractionColors.LightDark(0x0F000000, 0x0BFFFFFF));
        var outline = ResolveBrushColor(view.Foreground, UIColor.Label);
        var renderer = new UIGraphicsImageRenderer(UncheckedImage.Size);
        return renderer.CreateImage(_ =>
        {
            var bounds = new CoreGraphics.CGRect(CoreGraphics.CGPoint.Empty, UncheckedImage.Size);
            BoxFillImage.ApplyTintColor(fill, UIImageRenderingMode.AlwaysOriginal).Draw(bounds);
            UncheckedImage.ApplyTintColor(outline, UIImageRenderingMode.AlwaysOriginal).Draw(bounds);
        }).ImageWithRenderingMode(UIImageRenderingMode.AlwaysOriginal);
    }

    public static void MapColor(CheckBoxHandler handler, CheckBox view)
    {
        // UIColor.Label, not an accent: a check box's caption is body text.
        var color = ResolveBrushColor(view.Foreground, UIColor.Label);
        handler.PlatformView.SetTitleColor(color, UIControlState.Normal);
        ApplyGlyphTint(handler, view);
    }

    // The box glyph is an SF Symbol drawn in the button's tint. Unchecked it matches the caption;
    // checked, a null tint inherits the window's, i.e. the accent (and follows it when it changes).
    private static void ApplyGlyphTint(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.TintColor = view.IsChecked ? null : ResolveBrushColor(view.Foreground, UIColor.Label);
    }

    private static UIColor ResolveBrushColor(global::Microsoft.Maui.Controls.SolidColorBrush? brush, UIColor fallbackColor)
    {
        var color = brush?.Color;
        return color is null ? fallbackColor : color.ToPlatform();
    }

}