using CoreAnimation;
using CoreGraphics;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public sealed class ToggleButtonHandler : ViewHandler<ToggleButton, ToggleButtonHandler.ToggleButtonPlatformView>
{
    public static PropertyMapper<ToggleButton, ToggleButtonHandler> PropertyMapper = new(ViewMapper)
    {
        [nameof(ToggleButton.Text)] = MapText,
        [nameof(ToggleButton.FontSize)] = MapFontSize,
        [nameof(ToggleButton.Padding)] = MapPadding,
        [nameof(ToggleButton.HorizontalContentAlignment)] = MapHorizontalContentAlignment,
        [nameof(ToggleButton.BorderThickness)] = MapBorderThickness,
        [nameof(ToggleButton.BorderBrush)] = MapBorderBrush,
        [nameof(ToggleButton.Foreground)] = MapColor,
        [nameof(ToggleButton.IconGlyph)] = MapImageSource,
        [nameof(ToggleButton.IsChecked)] = MapIsSelected,
        [nameof(ToggleButton.ImageSpacing)] = MapImageSpacing,
    };

    public ToggleButtonHandler() : base(PropertyMapper) { }

    protected override void ConnectHandler(ToggleButtonPlatformView platformView)
    {
        base.ConnectHandler(platformView);

        ConfigureButton(platformView);
        platformView.AddTarget(ButtonTapped, UIControlEvent.TouchUpInside);

        UpdateButtonContent(platformView, VirtualView);
        UpdateButtonAppearance(platformView, VirtualView);
    }

    private void ConfigureButton(ToggleButtonPlatformView button)
    {
        button.ChangesSelectionAsPrimaryAction = true;
        button.Configuration = UIButtonConfiguration.PlainButtonConfiguration;
        button.ClipsToBounds = true;
        button.Layer.MasksToBounds = true;
        button.Layer.CornerRadius = 14;
        button.TitleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
    }

    protected override ToggleButtonPlatformView CreatePlatformView()
    {
        var button = new ToggleButtonPlatformView();
        button.ChangesSelectionAsPrimaryAction = true;
        return button;
    }

    protected override void DisconnectHandler(ToggleButtonPlatformView platformView)
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

    public static void MapFontSize(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
    }

    public static void MapPadding(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
    }

    public static void MapHorizontalContentAlignment(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonContent(handler.PlatformView, view);
    }

    public static void MapBorderThickness(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
    }

    public static void MapBorderBrush(ToggleButtonHandler handler, ToggleButton view)
    {
        UpdateButtonAppearance(handler.PlatformView, view);
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

    private static void UpdateButtonContent(ToggleButtonPlatformView button, ToggleButton view)
    {
        var configuration = button.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
        var foregroundColor = view.Foreground.Color.ToPlatform();

        configuration.Title = view.Text;
        configuration.Image = CreateImage(view, foregroundColor);
        configuration.ImagePlacement = NSDirectionalRectEdge.Leading;
        configuration.ImagePadding = (nfloat)Math.Max(0, view.ImageSpacing);
        configuration.BaseForegroundColor = foregroundColor;
        configuration.ContentInsets = ResolveContentInsets(view.Padding);

        button.Configuration = configuration;
        button.HorizontalAlignment = ResolveContentHorizontalAlignment(view.HorizontalContentAlignment);
        var currentFont = button.TitleLabel?.Font;
        button.TitleLabel.Font = currentFont?.WithSize((nfloat)view.FontSize) ?? UIFont.SystemFontOfSize((nfloat)view.FontSize);
        button.SetNeedsLayout();
    }

    private static void UpdateButtonAppearance(ToggleButtonPlatformView button, ToggleButton view)
    {
        var foregroundColor = view.Foreground.Color.ToPlatform();
        var baseBackgroundColor = ResolveBackgroundColor(view);

        button.TintColor = foregroundColor;
        button.UpdateBorderAppearance(view.BorderThickness, ResolveBorderColor(view, foregroundColor));
        button.BackgroundColor = view.IsChecked ? foregroundColor.ColorWithAlpha(0.16f) : baseBackgroundColor;
        button.Alpha = button.Enabled ? 1f : 0.55f;
    }

    private static NSDirectionalEdgeInsets ResolveContentInsets(Thickness padding)
    {
        return new NSDirectionalEdgeInsets(
            (nfloat)Math.Max(0, padding.Top),
            (nfloat)Math.Max(0, padding.Left),
            (nfloat)Math.Max(0, padding.Bottom),
            (nfloat)Math.Max(0, padding.Right));
    }

    private static UIControlContentHorizontalAlignment ResolveContentHorizontalAlignment(LayoutOptions alignment)
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

    private static UIColor ResolveBorderColor(ToggleButton view, UIColor fallbackColor)
    {
        if (view.BorderBrush is SolidColorBrush borderBrush)
        {
            return borderBrush.Color.ToPlatform();
        }

        return view.IsChecked ? fallbackColor.ColorWithAlpha(0.75f) : fallbackColor.ColorWithAlpha(0.35f);
    }

    public sealed class ToggleButtonPlatformView : UIButton
    {
        private readonly CALayer _topBorderLayer = CreateBorderLayer();
        private readonly CALayer _rightBorderLayer = CreateBorderLayer();
        private readonly CALayer _bottomBorderLayer = CreateBorderLayer();
        private readonly CALayer _leftBorderLayer = CreateBorderLayer();

        private Thickness _borderThickness;
        private UIColor? _borderColor;

        public ToggleButtonPlatformView() : base(UIButtonType.System)
        {
            Layer.AddSublayer(_topBorderLayer);
            Layer.AddSublayer(_rightBorderLayer);
            Layer.AddSublayer(_bottomBorderLayer);
            Layer.AddSublayer(_leftBorderLayer);
        }

        public void UpdateBorderAppearance(Thickness borderThickness, UIColor borderColor)
        {
            _borderThickness = borderThickness;
            _borderColor = borderColor;
            ApplyBorderAppearance();
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            ApplyBorderAppearance();
        }

        private void ApplyBorderAppearance()
        {
            if (_borderColor == null)
            {
                return;
            }

            var normalizedThickness = NormalizeThickness(_borderThickness);
            if (HasUniformThickness(normalizedThickness, out var uniformThickness))
            {
                Layer.BorderWidth = uniformThickness;
                Layer.BorderColor = _borderColor.CGColor;
                HideEdgeBorders();
                return;
            }

            Layer.BorderWidth = 0;
            Layer.BorderColor = null;

            UpdateEdgeBorder(_topBorderLayer, 0, 0, Bounds.Width, (nfloat)normalizedThickness.Top, _borderColor);
            UpdateEdgeBorder(_rightBorderLayer, Bounds.Width - (nfloat)normalizedThickness.Right, 0, (nfloat)normalizedThickness.Right, Bounds.Height, _borderColor);
            UpdateEdgeBorder(_bottomBorderLayer, 0, Bounds.Height - (nfloat)normalizedThickness.Bottom, Bounds.Width, (nfloat)normalizedThickness.Bottom, _borderColor);
            UpdateEdgeBorder(_leftBorderLayer, 0, 0, (nfloat)normalizedThickness.Left, Bounds.Height, _borderColor);
        }

        private void HideEdgeBorders()
        {
            _topBorderLayer.Hidden = true;
            _rightBorderLayer.Hidden = true;
            _bottomBorderLayer.Hidden = true;
            _leftBorderLayer.Hidden = true;
        }

        private static CALayer CreateBorderLayer()
        {
            return new CALayer
            {
                Hidden = true,
            };
        }

        private static Thickness NormalizeThickness(Thickness thickness)
        {
            return new Thickness(
                Math.Max(0, thickness.Left),
                Math.Max(0, thickness.Top),
                Math.Max(0, thickness.Right),
                Math.Max(0, thickness.Bottom));
        }

        private static bool HasUniformThickness(Thickness thickness, out nfloat uniformThickness)
        {
            uniformThickness = (nfloat)thickness.Left;
                 const double tolerance = 0.001;
                 return Math.Abs(thickness.Left - thickness.Top) < tolerance &&
                     Math.Abs(thickness.Left - thickness.Right) < tolerance &&
                     Math.Abs(thickness.Left - thickness.Bottom) < tolerance;
        }

        private static void UpdateEdgeBorder(CALayer borderLayer, nfloat x, nfloat y, nfloat width, nfloat height, UIColor color)
        {
            if (width <= 0 || height <= 0)
            {
                borderLayer.Hidden = true;
                return;
            }

            borderLayer.Hidden = false;
            borderLayer.BackgroundColor = color.CGColor;
            borderLayer.Frame = new CGRect(x, y, width, height);
        }
    }

}
