
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
        [nameof(ToggleButton.TintColor)] = MapColor,
        [nameof(ToggleButton.IconGlyph)] = MapImageSource,
        [nameof(ToggleButton.IsChecked)] = MapIsSelected
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
        if(handler.PlatformView.Selected != view.IsChecked)
            handler.PlatformView.Selected = view.IsChecked;
    }

    public static void MapText(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.PlatformView.SetTitle(view.Text, UIControlState.Normal);
    }

    public static void MapColor(ToggleButtonHandler handler, ToggleButton view)
    {
        handler.PlatformView.SetTitleColor(handler.VirtualView.TintColor.ToPlatform(), UIControlState.Normal);
    }

    public static async void MapImageSource(ToggleButtonHandler handler, ToggleButton view)
    {
        // if (view.ImageSource != null)
        // {
        //     var imageSourceService = handler.GetRequiredService<IImageSourceService>();
        //     var result = await imageSourceService.GetImageAsync(view.ImageSource);

        //     if (result?.Value != null)
        //     {
        //         handler.PlatformView.SetImage(result.Value, UIControlState.Normal);
        //     }
        // }
        // else
        // {
        //     handler.PlatformView.SetImage(null, UIControlState.Normal);
        // }
        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var image = UIImage.GetSystemImage(view.IconGlyph, config);
        handler.PlatformView.SetImage(image, UIControlState.Normal);
    }
}
