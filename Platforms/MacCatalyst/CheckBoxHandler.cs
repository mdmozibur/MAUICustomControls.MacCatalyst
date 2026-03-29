

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

        // Set up the button configuration
        platformView.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        
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

    public static void MapFontSize(CheckBoxHandler handler, CheckBox view)
    {
        var currentFont = handler.PlatformView.TitleLabel?.Font;
        handler.PlatformView.TitleLabel.Font = currentFont?.WithSize((nfloat)view.FontSize) ?? UIFont.SystemFontOfSize((nfloat)view.FontSize);
    }

    public static void MapBorderThickness(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.Layer.BorderWidth = (float)view.BorderThickness;
    }

    public static void MapIsSelected(CheckBoxHandler handler, CheckBox view)
    {
        var platformView = handler.PlatformView;
        platformView.SetImage(view.IsChecked ? CheckedImage : UncheckedImage, UIControlState.Normal);
    }
    
    public static void MapColor(CheckBoxHandler handler, CheckBox view)
    {
        handler.PlatformView.SetTitleColor(ResolveBrushColor(view.Foreground, global::Microsoft.Maui.Graphics.Colors.DodgerBlue.ToPlatform()), UIControlState.Normal);
    }

    private static UIColor ResolveBrushColor(global::Microsoft.Maui.Controls.SolidColorBrush? brush, UIColor fallbackColor)
    {
        var color = brush?.Color;
        return color is null ? fallbackColor : color.ToPlatform();
    }

}