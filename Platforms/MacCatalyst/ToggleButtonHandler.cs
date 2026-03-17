
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

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

        ConfigureButton(platformView);
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);

        UpdateButtonContent(platformView, VirtualView);
        UpdateButtonAppearance(platformView, VirtualView);
    }

    private void ConfigureButton(UIButton button)
    {
        button.ChangesSelectionAsPrimaryAction = true;
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        button.ClipsToBounds = true;
        button.Layer.CornerRadius = 14;
        button.Layer.BorderWidth = 1;
        button.TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
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

    private void ButtonTapped(object? sender, EventArgs e)
    {
        if (VirtualView.IsChecked != PlatformView.Selected)
        {
            VirtualView.IsChecked = PlatformView.Selected;
        }

        UpdateButtonAppearance(PlatformView, VirtualView);
    }

    public static void MapIsSelected(ToggleButtonHandler handler, ToggleButton view)
    {
        if (handler.PlatformView.Selected != view.IsChecked)
        {
            handler.PlatformView.Selected = view.IsChecked;
        }

        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapText(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
    }

    public static void MapColor(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapImageSource(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
    }

    private static void MapImageSpacing(ToggleButtonHandler handler, ToggleButton button)
    {
        UpdateButtonContent(handler.PlatformView, button);
    }

    private static void UpdateButtonContent(UIButton button, ToggleButton view)
    {
        var configuration = button.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
        var foregroundColor = view.Foreground.Color.ToPlatform();

        configuration.Title = view.Text;
        configuration.Image = CreateImage(view, foregroundColor);
        configuration.ImagePlacement = NSDirectionalRectEdge.Leading;
        configuration.ImagePadding = (nfloat)Math.Max(0, view.ImageSpacing);
        configuration.BaseForegroundColor = foregroundColor;
        configuration.ContentInsets = new NSDirectionalEdgeInsets(10, 14, 10, 14);

        button.Configuration = configuration;
        button.SetNeedsLayout();
    }

    private static void UpdateButtonAppearance(UIButton button, ToggleButton view)
    {
        var foregroundColor = view.Foreground.Color.ToPlatform();
        var baseBackgroundColor = ResolveBackgroundColor(view);

        button.TintColor = foregroundColor;
        button.Layer.BorderColor = (view.IsChecked ? foregroundColor.ColorWithAlpha(0.75f) : foregroundColor.ColorWithAlpha(0.35f)).CGColor;
        button.BackgroundColor = view.IsChecked ? foregroundColor.ColorWithAlpha(0.16f) : baseBackgroundColor;
        button.Alpha = button.Enabled ? 1f : 0.55f;
    }

    private static UIImage? CreateImage(ToggleButton view, UIColor tintColor)
    {
        if (string.IsNullOrWhiteSpace(view.IconGlyph))
        {
            return null;
        }

        var config = UIImageSymbolConfiguration.Create(UIImageSymbolScale.Medium);
        var image = UIImage.GetSystemImage(view.IconGlyph, config);
        return image?.ApplyTintColor(tintColor, UIImageRenderingMode.AlwaysOriginal);
    }

    private static UIColor ResolveBackgroundColor(ToggleButton view)
    {
        if (view.Background is SolidColorBrush backgroundBrush)
        {
            return backgroundBrush.Color.ToPlatform();
        }

        return UIColor.Clear;
    }

}
