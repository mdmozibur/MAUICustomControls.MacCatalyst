using CoreAnimation;
using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public static class EntryHandlerCustomization
{
    private const string BottomBorderLayerName = "UwpStyleBottomBorder";
    private static readonly nfloat CornerRadius = 2f;
    private static readonly nfloat BorderWidth = 1f;

    public static void Configure()
    {
        EntryHandler.Mapper.AppendToMapping("UwpStyleEntry", (handler, view) =>
        {
            var textField = handler.PlatformView;
            ApplyBaseStyle(textField);
        });
    }

    private static void ApplyBaseStyle(UITextField textField)
    {
        // Remove default thick rounded border
        textField.BorderStyle = UITextBorderStyle.None;
        textField.Layer.CornerRadius = CornerRadius;
        textField.Layer.MasksToBounds = true;

        // Subtle border for unfocused state
        textField.Layer.BorderColor = UIColor.Separator.CGColor;
        textField.Layer.BorderWidth = 0.5f;

        // Add small padding so text doesn't touch edges
        textField.LeftView = new UIView(new CGRect(0, 0, 4, 0));
        textField.LeftViewMode = UITextFieldViewMode.Always;
        textField.RightView = new UIView(new CGRect(0, 0, 4, 0));
        textField.RightViewMode = UITextFieldViewMode.Always;

        // Subscribe to focus events
        textField.EditingDidBegin -= OnEditingDidBegin;
        textField.EditingDidEnd -= OnEditingDidEnd;
        textField.EditingDidBegin += OnEditingDidBegin;
        textField.EditingDidEnd += OnEditingDidEnd;
    }

    private static void OnEditingDidBegin(object? sender, EventArgs e)
    {
        if (sender is not UITextField textField)
            return;

        // Remove subtle all-around border
        textField.Layer.BorderWidth = 0;

        // Add 1px bottom border (UWP focused TextBox style)
        RemoveBottomBorder(textField);
        var bottomBorder = new CALayer
        {
            Name = BottomBorderLayerName,
            BackgroundColor = UIColor.SystemBlue.CGColor,
            Frame = new CGRect(0, textField.Bounds.Height - BorderWidth, textField.Bounds.Width, BorderWidth)
        };
        textField.Layer.AddSublayer(bottomBorder);

        // Ensure border repositions on layout changes
        textField.SetNeedsLayout();
    }

    private static void OnEditingDidEnd(object? sender, EventArgs e)
    {
        if (sender is not UITextField textField)
            return;

        // Remove bottom border
        RemoveBottomBorder(textField);

        // Restore subtle all-around border
        textField.Layer.BorderColor = UIColor.Separator.CGColor;
        textField.Layer.BorderWidth = 0.5f;
    }

    private static void RemoveBottomBorder(UITextField textField)
    {
        if (textField.Layer.Sublayers is null)
            return;

        foreach (var layer in textField.Layer.Sublayers)
        {
            if (layer.Name == BottomBorderLayerName)
            {
                layer.RemoveFromSuperLayer();
                break;
            }
        }
    }
}
