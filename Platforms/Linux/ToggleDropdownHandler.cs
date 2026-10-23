namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MAUICustomControls.MacCatalyst.Controls.CustomObjects;
using AvaloniaViewHandler = Avalonia.Controls.Maui.Handlers.ViewHandler;
using ToggleDropdown = MAUICustomControls.MacCatalyst.Controls.ToggleDropdown;

/// <summary>
/// UWP's ToggleDropdown on Linux: a <see cref="PressableFace"/> with the selected option's icon and
/// the title. A click checks an unchecked dropdown; clicking it once checked opens its options in a
/// menu (an action menu opens them on every click), and a chevron in the corner says so.
/// </summary>
public sealed class ToggleDropdownHandler : Avalonia.Controls.Maui.Handlers.ViewHandler<ToggleDropdown, PressableFace>
{
    public static readonly PropertyMapper<ToggleDropdown, ToggleDropdownHandler> Mapper = new(AvaloniaViewHandler.ViewMapper)
    {
        [nameof(ToggleDropdown.IsChecked)] = MapAll,
        [nameof(ToggleDropdown.TintColor)] = MapAll,
        [nameof(ToggleDropdown.TextColor)] = MapAll,
        [nameof(ToggleDropdown.BorderColor)] = MapAll,
        [nameof(ToggleDropdown.BorderThickness)] = MapAll,
        [nameof(ToggleDropdown.CornerRadius)] = MapAll,
        [nameof(ToggleDropdown.DropdownIndicatorGlyph)] = MapAll,
        [nameof(ToggleDropdown.DropdownIndicatorFontFamily)] = MapAll,
        [nameof(ToggleDropdown.HorizontalContentAlignment)] = MapAll,
        [nameof(ToggleDropdown.UnselectedText)] = MapAll,
        [nameof(ToggleDropdown.Spacing)] = MapAll,
        [nameof(ToggleDropdown.FontSize)] = MapAll,
        [nameof(ToggleDropdown.Orientation)] = MapAll,
        [nameof(ToggleDropdown.IconFontSize)] = MapAll,
        [nameof(ToggleDropdown.IconFontWeight)] = MapAll,
        [nameof(ToggleDropdown.Options)] = MapAll,
        [nameof(ToggleDropdown.SelectedOption)] = MapAll,
        [nameof(ToggleDropdown.ChangeUnselectedTextOnSelectionChange)] = MapAll,
        [nameof(ToggleDropdown.IsActionMenu)] = MapAll,
        [nameof(ToggleDropdown.IconGlyph)] = MapAll,
        [nameof(ToggleDropdown.IconFontFamily)] = MapAll,
        [nameof(ToggleDropdown.Padding)] = MapAll,
        [nameof(IView.Background)] = MapAll,
    };

    // Set while MAUI maps every property of a new virtual view: each mapping rebuilds the face, so
    // it is built once when SetVirtualView has mapped everything.
    private bool _settingVirtualView;

    public ToggleDropdownHandler() : base(Mapper, AvaloniaViewHandler.ViewCommandMapper)
    {
    }

    public override void SetVirtualView(IView view)
    {
        _settingVirtualView = true;
        try
        {
            base.SetVirtualView(view);
        }
        finally
        {
            _settingVirtualView = false;
        }

        if (VirtualView is { } dropdown)
        {
            Rebuild(dropdown);
        }
    }

    protected override PressableFace CreatePlatformView() => new();

    protected override void ConnectHandler(PressableFace platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Click += OnClick;
        platformView.ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void DisconnectHandler(PressableFace platformView)
    {
        platformView.Click -= OnClick;
        platformView.ActualThemeVariantChanged -= OnThemeChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (VirtualView is { } dropdown)
        {
            Rebuild(dropdown);
        }
    }

    private static void MapAll(ToggleDropdownHandler handler, ToggleDropdown dropdown)
    {
        if (!handler._settingVirtualView)
        {
            handler.Rebuild(dropdown);
        }
    }

    private void OnClick(object? sender, EventArgs e)
    {
        if (VirtualView is not { IsEnabled: true } dropdown)
        {
            return;
        }

        if (!dropdown.IsActionMenu && !dropdown.IsChecked)
        {
            dropdown.MarkNextToggleAsUserInitiated();
            dropdown.IsChecked = true;
            return;
        }

        if (dropdown.Options is { Count: > 0 } options && (dropdown.IsActionMenu || options.Count > 1))
        {
            ShowOptions(dropdown, options);
        }
    }

    private void ShowOptions(ToggleDropdown dropdown, IList<SelectorOption> options)
    {
        var selected = dropdown.IsActionMenu || !dropdown.SelectedOption.HasValue
            ? -1
            : options.IndexOf(dropdown.SelectedOption.Value);

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var chosen = index;
            var item = new MenuItem
            {
                Header = option.Text,
                ToggleType = dropdown.IsActionMenu ? MenuItemToggleType.None : MenuItemToggleType.Radio,
                IsChecked = index == selected,
            };
            if (!string.IsNullOrEmpty(option.IconGlyph))
            {
                item.Icon = new TextBlock
                {
                    Text = option.IconGlyph,
                    FontSize = option.IconFontSize > 0 ? option.IconFontSize : 14,
                    FontFamily = LinuxControlSupport.ResolveFontFamily(MauiContext, option.IconFontFamily) ?? FontFamily.Default,
                };
            }

            item.Click += (_, _) => OptionChosen(chosen);
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => PlatformView.IsHeld = false;
        PlatformView.IsHeld = true;
        menu.ShowAt(PlatformView);
    }

    private void OptionChosen(int index)
    {
        if (VirtualView is not { } dropdown || index < 0 || index >= dropdown.Options.Count)
        {
            return;
        }

        var selectedOption = dropdown.Options[index];
        dropdown.MarkNextSelectionAsUserInitiated();
        if (!dropdown.IsActionMenu)
        {
            dropdown.MarkNextToggleAsUserInitiated();
            dropdown.IsChecked = true;
        }

        dropdown.SelectedOption = selectedOption;
        if (dropdown.IsActionMenu)
        {
            dropdown.SetSelectedOptionFromCompatibility(null, true);
        }
    }

    private void Rebuild(ToggleDropdown dropdown)
    {
        var face = PlatformView;
        if (face is null)
        {
            return;
        }

        // UWP template: the theme's foreground normally, the accent only when checked.
        var foreground = dropdown.IsChecked
            ? new SolidColorBrush(dropdown.TintColor.ToAvalonia())
            : dropdown.TextColor is { } textColor ? new SolidColorBrush(textColor.ToAvalonia()) : null;

        var title = dropdown.IsActionMenu
            ? dropdown.UnselectedText
            : dropdown.ChangeUnselectedTextOnSelectionChange ? (dropdown.SelectedOption?.Text ?? dropdown.UnselectedText) : dropdown.UnselectedText;

        var (glyph, glyphFamily, glyphSize) = SelectedGlyph(dropdown);
        var content = LinuxControlSupport.BuildGlyphAndText(
            MauiContext,
            glyph,
            glyphFamily,
            glyphSize,
            title,
            dropdown.FontSize,
            dropdown.Orientation == Microsoft.Maui.Controls.StackOrientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
            dropdown.Spacing,
            foreground,
            dropdown.IconFontWeight.HasFlag(Microsoft.Maui.Controls.FontAttributes.Bold) ? FontWeight.SemiBold : FontWeight.Normal);
        content.HorizontalAlignment = dropdown.HorizontalContentAlignment.ToHorizontalAlignment();

        var root = new Grid();
        root.Children.Add(content);

        var hasMenu = dropdown.Options is { Count: > 1 } && !dropdown.IsActionMenu;
        if (hasMenu && dropdown.IsChecked)
        {
            var indicator = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(dropdown.DropdownIndicatorGlyph) ? "▾" : dropdown.DropdownIndicatorGlyph,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Avalonia.Thickness(0, 0, -2, -2),
            };
            if (LinuxControlSupport.ResolveFontFamily(MauiContext, dropdown.DropdownIndicatorFontFamily, 10) is { } family)
            {
                indicator.FontFamily = family;
            }

            if (foreground is not null)
            {
                indicator.Foreground = foreground;
            }

            root.Children.Add(indicator);
        }

        face.Child = root;
        face.Padding = dropdown.Padding.ToAvalonia();
        face.CornerRadius = new Avalonia.CornerRadius(dropdown.CornerRadius);
        face.BorderThickness = new Avalonia.Thickness(dropdown.BorderThickness);
        face.BorderBrush = dropdown.BorderColor is { } border ? new SolidColorBrush(border.ToAvalonia()) : null;
        face.FaceBackground = dropdown.Background.ToAvaloniaBrush();
    }

    private static (string? Glyph, string? Family, double Size) SelectedGlyph(ToggleDropdown dropdown)
    {
        if (!string.IsNullOrWhiteSpace(dropdown.IconGlyph) && !string.IsNullOrWhiteSpace(dropdown.IconFontFamily))
        {
            return (dropdown.IconGlyph, dropdown.IconFontFamily, dropdown.IconFontSize);
        }

        if (dropdown.SelectedOption is { } option && !string.IsNullOrWhiteSpace(option.IconGlyph) && !string.IsNullOrWhiteSpace(option.IconFontFamily))
        {
            return (option.IconGlyph, option.IconFontFamily, dropdown.IconFontSize > 0 ? dropdown.IconFontSize : option.IconFontSize);
        }

        return (null, null, 0);
    }
}
