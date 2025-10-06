
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ComboBoxHandler : ViewHandler<ComboBox, UIButton>
{
    public static PropertyMapper<ComboBox, ComboBoxHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ComboBox.UnselectedText)] = MapText,
        [nameof(ComboBox.BorderThickness)] = MapText,
        [nameof(ComboBox.Options)] = VirtualView_Options_CollectionChanged,
        [nameof(ComboBox.SelectedOption)] = MapSelectedOption
    };

    public ComboBoxHandler() : base(PropertyMapper) { }

    protected override void ConnectHandler(UIButton platformView)
    {
        base.ConnectHandler(platformView);

        MapText(this, VirtualView);
        MapBorderThickness(this, VirtualView);
    }

    protected override void DisconnectHandler(UIButton platformView)
    {
        base.DisconnectHandler(platformView);
    }

    private void OptionChosen(UIAction action)
    {
        var selectedOption = VirtualView.Options.FirstOrDefault(o => o.Text == action.Title);

        var selectedMenuItem = PlatformView.Menu?.Children
            .OfType<UIAction>()
            .FirstOrDefault(item => item.State == UIMenuElementState.On);

        if (selectedMenuItem is not null)
            selectedMenuItem.State = UIMenuElementState.Off;

        action.State = UIMenuElementState.On;

        var actual_action = PlatformView.Menu?.Children
            .OfType<UIAction>()
            .FirstOrDefault(item => item.Identifier == action.Identifier);

        if (actual_action is not null)
            actual_action.State = UIMenuElementState.On;

        VirtualView.SelectedOption = selectedOption;
    }

    protected override UIButton CreatePlatformView()
    {
        var button = new UIButton(UIButtonType.System);
        button.ShowsMenuAsPrimaryAction = true;
        return button;
    }

    public static void MapText(ComboBoxHandler handler, ComboBox view)
    {
        handler.PlatformView.SetTitle(view.UnselectedText, UIControlState.Normal);
    }

    public static void MapBorderThickness(ComboBoxHandler handler, ComboBox view)
    {
        handler.PlatformView.Layer.BorderWidth = (float)view.BorderThickness;
    }

    public static void MapSelectedOption(ComboBoxHandler handler, ComboBox view)
    {
        if (view.SelectedOption.HasValue)
        {
            var selectedMenuItem = handler.PlatformView.Menu?.Children
                .OfType<UIAction>()
                .FirstOrDefault(item => item.Title == view.SelectedOption.Value.Text);

            if (selectedMenuItem is null)
                return;

            handler.PlatformView.SetImage(selectedMenuItem.Image, UIControlState.Normal);
            handler.PlatformView.SetTitle(selectedMenuItem.Title, UIControlState.Normal);
        }
        else
        {
            handler.PlatformView.SetImage(null, UIControlState.Normal);
            handler.PlatformView.SetTitle(view.UnselectedText, UIControlState.Normal);
        }
    }
    
    private static void VirtualView_Options_CollectionChanged(ComboBoxHandler handler, ComboBox view)
    {
        // Rebuild the menu
        handler.PlatformView.Menu = null;

        var menuItems = new UIAction[view.Options.Count];
        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);

        for (int i = 0; i < view.Options.Count; i++)
        {
            var option = view.Options[i];
            var image = string.IsNullOrWhiteSpace(option.SystemIconName) ? null : UIImage.GetSystemImage(option.SystemIconName, config);
            menuItems[i] = UIAction.Create(option.Text, image, null, handler.OptionChosen);
        }

        var menu = UIMenu.Create(menuItems);
        handler.PlatformView.Menu = menu;
    }

}
