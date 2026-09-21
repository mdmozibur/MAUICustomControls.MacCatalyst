using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// Keeps UIKit controls drawing the app's own (WinUI) look when the app runs in the Mac idiom
/// (UIDeviceFamily 6, "Optimize for Mac").
///
/// In that idiom UIKit hands UIButton and UISwitch to AppKit-style renderers: a button becomes an
/// NSButton bezel that ignores its background, border and image layout, and a switch turns into a
/// checkbox. The iPad behavioural style keeps a button customisable while the window still renders
/// at 100 % and at the display's native scale (MAUI already does this for UISlider).
///
/// MAUI's ButtonHandler also branches on the Mac idiom: it maps Background and TextColor into a
/// UIButtonConfiguration (bordered style) instead of the view. A configuration owns the title, so
/// the button then ignores the Font (icon-font glyphs fell back to the system font), the legacy
/// edge insets ContentLayout uses (the icon moved beside the title), CornerRadius and the stroke.
/// With the iPad behavioural style the legacy path renders correctly, so those two mappings use it.
/// </summary>
public static class MacIdiomControlStyle
{
    public static void Configure()
    {
        if (UIDevice.CurrentDevice.UserInterfaceIdiom != UIUserInterfaceIdiom.Mac)
        {
            return;
        }

        // The behavioural style has to be chosen before the first title is set: switching it later
        // swaps UIButton's visual provider and the legacy title is no longer drawn. So the buttons
        // are created here, as MAUI creates them, with the style already set.
        ViewHandler<IButton, UIButton>.PlatformViewFactory = _ => CreatePadButton(clipsToBounds: false, copyAppearanceProxy: true);
        ViewHandler<IImageButton, UIButton>.PlatformViewFactory = _ => CreatePadButton(clipsToBounds: true, copyAppearanceProxy: false);
        ButtonHandler.Mapper.ModifyMapping(nameof(IView.Background), (handler, button, _) => handler.PlatformView?.UpdateBackground(button));
        ButtonHandler.Mapper.ModifyMapping(nameof(ITextStyle.TextColor), (handler, button, _) =>
        {
            if (button is ITextStyle textStyle)
            {
                handler.PlatformView?.UpdateTextColor(textStyle);
            }
        });
        SwitchHandler.Mapper.AppendToMapping("SlidingSwitchStyle", (handler, _) => Apply(handler.PlatformView));
        ButtonInteractionStates.Configure();
    }

    // Mirrors ButtonHandler/ImageButtonHandler.CreatePlatformView (a system button, the title colours
    // and background images of the UIButton appearance proxy) plus the iPad behavioural style.
    private static UIButton CreatePadButton(bool clipsToBounds, bool copyAppearanceProxy)
    {
        var button = new UIButton(UIButtonType.System) { ClipsToBounds = clipsToBounds };
        Apply(button);
        if (copyAppearanceProxy)
        {
            foreach (var state in new[] { UIControlState.Normal, UIControlState.Highlighted, UIControlState.Disabled })
            {
                button.SetTitleColor(UIButton.Appearance.TitleColor(state), state);
                button.SetTitleShadowColor(UIButton.Appearance.TitleShadowColor(state), state);
                button.SetBackgroundImage(UIButton.Appearance.BackgroundImageForState(state), state);
            }
        }

        return button;
    }

    public static void Apply(UIButton button)
    {
        if (button.PreferredBehavioralStyle != UIBehavioralStyle.Pad)
        {
            button.PreferredBehavioralStyle = UIBehavioralStyle.Pad;
        }
    }

    /// <summary>WinUI's ToggleSwitch is a sliding switch; the Mac idiom would draw a checkbox.</summary>
    public static void Apply(UISwitch toggle)
    {
        if (toggle.PreferredStyle != UISwitchStyle.Sliding)
        {
            toggle.PreferredStyle = UISwitchStyle.Sliding;
        }
    }
}
