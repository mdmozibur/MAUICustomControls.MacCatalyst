
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MAUICustomControls.MacCatalyst.Controls;
using UIKit;

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

    public static void MapBorderThickness(ToggleDropdownHandler handler, ToggleDropdown view)
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
        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var menuItems = new UIAction[view.Options.Count];

        for (int i = 0; i < view.Options.Count; i++)
        {
            var option = view.Options[i];
            var image = string.IsNullOrWhiteSpace(option.SystemIconName) ? null : UIImage.GetSystemImage(option.SystemIconName, config);
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
        var title = view.SelectedOption?.Text ?? view.UnselectedText;
        var image = CreateSelectedImage(view.SelectedOption, tintColor);

        configuration.Title = title;
        configuration.Image = image;
        configuration.ImagePlacement = NSDirectionalRectEdge.Leading;
        configuration.ImagePadding = image is null ? 0 : 8;
        configuration.BaseForegroundColor = tintColor;
        configuration.ContentInsets = new NSDirectionalEdgeInsets(10, 14, 10, 14);

        button.Configuration = configuration;
        button.Selected = view.IsChecked;
        button.ShowsMenuAsPrimaryAction = view.IsChecked && button.Menu is not null;
        button.Layer.BorderWidth = (float)view.BorderThickness;
        button.Layer.BorderColor = tintColor.ColorWithAlpha(view.IsChecked ? 0.75f : 0.35f).CGColor;
        button.BackgroundColor = view.IsChecked ? tintColor.ColorWithAlpha(0.14f) : UIColor.Clear;
    }

    private static UIImage? CreateSelectedImage(Controls.CustomObjects.SelectorOption? selectedOption, UIColor tintColor)
    {
        if (!selectedOption.HasValue || string.IsNullOrWhiteSpace(selectedOption.Value.SystemIconName))
        {
            return null;
        }

        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var image = UIImage.GetSystemImage(selectedOption.Value.SystemIconName, config);
        return image?.ApplyTintColor(tintColor, UIImageRenderingMode.AlwaysOriginal);
    }

}