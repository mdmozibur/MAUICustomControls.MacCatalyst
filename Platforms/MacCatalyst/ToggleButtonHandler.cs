
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using UIKit;
using Microsoft.Maui.Platform;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleButtonHandler : ViewHandler<ToggleButton, UIButton>
{
    public static PropertyMapper<ToggleButton, ToggleButtonHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ToggleButton.Text)] = MapText,
        [nameof(ToggleButton.Foreground)] = MapColor,
        [nameof(ToggleButton.IconGlyph)] = MapImageSource,
        [nameof(ToggleButton.IsChecked)] = MapIsSelected,
        [nameof(ToggleButton.ImageSpacing)] = MapImageSpacing,
    };

    public ToggleButtonHandler() : base(PropertyMapper) { }
    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        // Set up the button configuration
        ConfigureButton(platformView);

        // Use the proper event to handle selection changes
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);

        MapText(this, VirtualView);
        MapImageSource(this, VirtualView);
        MapIsSelected(this, VirtualView);
    }

    private void ConfigureButton(UIButton button)
    {
        button.ChangesSelectionAsPrimaryAction = true;

        // If you want to change background colors
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton(UIButtonType.System);
        button.ChangesSelectionAsPrimaryAction = true;
        return button;
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        platformView.RemoveTarget(ButtonTapped, UIControlEvent.TouchUpInside);
        base.DisconnectHandler(platformView);
    }

    private void ButtonTapped(object sender, EventArgs e)
    {
        // Sync the platform view's selected state back to the virtual view
        VirtualView.IsChecked = PlatformView.Selected;
    }

    public static void MapIsSelected(ToggleButtonHandler handler, ToggleButton view)
    {
        if (handler.PlatformView.Selected != view.IsChecked)
            handler.PlatformView.Selected = view.IsChecked;
    }

    public static void MapText(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.PlatformView.SetTitle(view.Text, UIControlState.Normal);
    }

    public static void MapColor(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.PlatformView.SetTitleColor(handler.VirtualView.Foreground.Color.ToPlatform(), UIControlState.Normal);
    }

    public static async void MapImageSource(ToggleButtonHandler handler, ToggleButton view)
    {
        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var image = UIImage.GetSystemImage(view.IconGlyph, config);
        image.ApplyTintColor(view.Foreground.Color.ToPlatform());
        handler.PlatformView.SetImage(image, UIControlState.Normal);
    }
    

    private static void MapImageSpacing(ToggleButtonHandler handler, ToggleButton button)
    {
        throw new NotImplementedException();
    }

}
