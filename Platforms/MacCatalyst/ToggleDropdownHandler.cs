

using Microsoft.Maui.Handlers;
using UIKit;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using ObjCRuntime;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleDropdownHandler : ViewHandler<ToggleDropdown, UIButton>
{
    public static PropertyMapper<ToggleDropdown, ToggleDropdownHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ToggleDropdown.UnselectedText)] = MapText,
        [nameof(ToggleDropdown.BorderThickness)] = MapBorderThickness,
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

        // Set up the button configuration
        platformView.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        
        // Use the proper event to handle selection changes
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);
        
        MapText(this, VirtualView);
        MapIsSelected(this, VirtualView);
    }

    private void ButtonTapped(object sender, EventArgs e)
    {
        // Sync the platform view's selected state back to the virtual view
        if (PlatformView.Selected && !VirtualView.IsChecked)
        {
            VirtualView.IsChecked = PlatformView.Selected;
            PlatformView.ShowsMenuAsPrimaryAction = true;
            PlatformView.ChangesSelectionAsPrimaryAction = false;

            if (VirtualView.SelectedOption is null)
            {
                PlatformView.SetTitle(VirtualView.UnselectedText, UIControlState.Normal);
            }
        }
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
        var button = new UIButton(UIButtonType.System)
        {
            ChangesSelectionAsPrimaryAction = true
        };
        return button;
    }

    public static void MapText(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        handler.PlatformView.SetTitle(view.UnselectedText, UIControlState.Normal);
    }

    public static void MapBorderThickness(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        handler.PlatformView.Layer.BorderWidth = (float)view.BorderThickness;
    }

    public static void MapSelectedOption(ToggleDropdownHandler handler, ToggleDropdown view)
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
    public static void MapIsSelected(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        if (handler.PlatformView.Selected != view.IsChecked)
            handler.PlatformView.Selected = view.IsChecked;

        if (!view.IsChecked)
        {
            handler.PlatformView.ShowsMenuAsPrimaryAction = false;
            handler.PlatformView.ChangesSelectionAsPrimaryAction = true;
            handler.PlatformView.Selected = false;
        }
    }
    
    public static void MapColor(ToggleDropdownHandler handler, ToggleDropdown view)
    {
        handler.PlatformView.SetTitleColor(handler.VirtualView.TintColor.ToPlatform(), UIControlState.Normal);
    }
    private static void VirtualView_Options_CollectionChanged(ToggleDropdownHandler handler, ToggleDropdown view)
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
        handler.PlatformView.ShowsMenuAsPrimaryAction = true;
    }

}