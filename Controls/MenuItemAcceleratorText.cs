namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// The text a menu item shows at its trailing edge instead of its shortcut (UWP
/// MenuFlyoutItem.KeyboardAcceleratorTextOverride: "Ctrl+S", or a state such as "On"). Native context
/// menus show it as the action's subtitle (Platforms/MacCatalyst/ContextMenuSupport.cs).
/// </summary>
public static class MenuItemAcceleratorText
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.CreateAttached("Text", typeof(string), typeof(MenuItemAcceleratorText), null);

    public static string? GetText(BindableObject item) => (string?)item.GetValue(TextProperty);

    public static void SetText(BindableObject item, string? value) => item.SetValue(TextProperty, value);

    /// <summary><paramref name="item"/> with the text set (for object initializers).</summary>
    public static T With<T>(T item, string? value) where T : BindableObject
    {
        SetText(item, value);
        return item;
    }
}
