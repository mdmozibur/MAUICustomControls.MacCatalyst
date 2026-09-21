using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// UWP's Control.UseSystemFocusVisuals for the custom controls: true (the default) draws the system
/// focus ring when the control has keyboard focus, false draws none (the control shows focus
/// itself, or not at all).
/// </summary>
public static class FocusVisuals
{
    public static readonly BindableProperty UseSystemFocusVisualsProperty = BindableProperty.CreateAttached(
        "UseSystemFocusVisuals", typeof(bool), typeof(FocusVisuals), true, propertyChanged: OnUseSystemFocusVisualsChanged);

    public static bool GetUseSystemFocusVisuals(BindableObject view) => (bool)view.GetValue(UseSystemFocusVisualsProperty);

    public static void SetUseSystemFocusVisuals(BindableObject view, bool value) => view.SetValue(UseSystemFocusVisualsProperty, value);

    private static void OnUseSystemFocusVisualsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Handlers re-read the property when they (re)build their native view; a later change is
        // pushed to the one that is already there.
        if (bindable is VisualElement { Handler: IElementHandler handler })
        {
            handler.UpdateValue("UseSystemFocusVisuals");
        }
    }
}
