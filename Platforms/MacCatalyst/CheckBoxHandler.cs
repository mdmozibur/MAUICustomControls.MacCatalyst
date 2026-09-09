

using Microsoft.Maui.Handlers;
using UIKit;
using Microsoft.Maui.Platform;
using CheckBox = MAUICustomControls.MacCatalyst.Controls.CheckBox;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class CheckBoxHandler : ViewHandler<CheckBox, UIButton>
{
    public static PropertyMapper<CheckBox, CheckBoxHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(CheckBox.Text)] = MapText,
        [nameof(CheckBox.FontSize)] = MapFontSize,
        [nameof(CheckBox.FontAttributes)] = MapFontAttributes,
        [nameof(CheckBox.BorderThickness)] = MapBorderThickness,
        [nameof(CheckBox.IsChecked)] = MapIsSelected,
        [nameof(CheckBox.Foreground)] = MapColor,
    };

    static UIImage CheckedImage = UIImage.GetSystemImage("checkmark.square");
    static UIImage UncheckedImage = UIImage.GetSystemImage("square");

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
        
        // Use the proper event to handle selection changes
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);
        
        MapText(this, VirtualView);
        MapIsSelected(this, VirtualView);
    }

    private void ButtonTapped(object sender, EventArgs e)
    {
        VirtualView.IsChecked = !VirtualView.IsChecked;
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton(UIButtonType.System);
        return button;
    }

    public static void MapText(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.SetTitle(view.Text, UIControlState.Normal);
    }

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
        platformView.SetImage(view.IsChecked ? CheckedImage : UncheckedImage, UIControlState.Normal);
    }
    
    public static void MapColor(CheckBoxHandler handler, CheckBox view)
    {
        // UIColor.Label, not an accent: a check box's caption is body text.
        var color = ResolveBrushColor(view.Foreground, UIColor.Label);
        handler.PlatformView.SetTitleColor(color, UIControlState.Normal);

        // The box glyph is an SF Symbol, which would otherwise take the button's tint.
        handler.PlatformView.TintColor = color;
    }

    private static UIColor ResolveBrushColor(global::Microsoft.Maui.Controls.SolidColorBrush? brush, UIColor fallbackColor)
    {
        var color = brush?.Color;
        return color is null ? fallbackColor : color.ToPlatform();
    }

}