using System.Runtime.CompilerServices;
using CoreAnimation;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Controls;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// The system keyboard-focus ring for the custom controls.
/// </summary>
/// <remarks>
/// With keyboard navigation on, UIKit draws a focus halo around a focused native control (UIButton,
/// UISwitch, UISegmentedControl). A custom UIView/UIControl is not focusable by default and draws
/// no halo, so it opts in (CanBecomeFocused) and describes its halo here: the view's bounds with its
/// layer's corner radius, refreshed from LayoutSubviews. UWP's UseSystemFocusVisuals="False"
/// (<see cref="FocusVisuals"/>) removes the ring from either kind.
/// </remarks>
internal static class FocusRing
{
    private static readonly ConditionalWeakTable<UIView, UIFocusEffect> SystemEffects = new();

    /// <summary>A custom view's halo, shaped like the view, or none when the element turned it off.</summary>
    public static void UpdateCustomView(UIView view, BindableObject? element)
    {
        if (element is not null && !FocusVisuals.GetUseSystemFocusVisuals(element))
        {
            view.FocusEffect = null;
            return;
        }

        view.FocusEffect = UIFocusHaloEffect.Create(view.Bounds, view.Layer.CornerRadius, CACornerCurve.Continuous.GetConstant()!);
    }

    /// <summary>A native control keeps the system's own ring unless the element turned it off.</summary>
    public static void UpdateSystemControl(UIView view, BindableObject? element)
    {
        var enabled = element is null || FocusVisuals.GetUseSystemFocusVisuals(element);
        if (!enabled)
        {
            if (view.FocusEffect is { } systemEffect)
            {
                SystemEffects.AddOrUpdate(view, systemEffect);
            }

            view.FocusEffect = null;
        }
        else if (view.FocusEffect is null && SystemEffects.TryGetValue(view, out var saved))
        {
            view.FocusEffect = saved;
        }
    }
}
