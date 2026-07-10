using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

public static class ContextMenuVisibilityHelper
{
    private static readonly Dictionary<object, List<IMenuElement>> OriginalItems = new();

    public static void Apply(MenuFlyout flyout)
    {
        Restore(flyout);
        Filter(GetItems(flyout));
    }

    private static void Restore(MenuFlyout flyout)
    {
        if (!OriginalItems.TryGetValue(flyout, out var originalItems))
        {
            originalItems = GetItems(flyout).ToList();
            OriginalItems[flyout] = originalItems;
        }

        var items = GetItems(flyout);
        items.Clear();
        foreach (var item in originalItems)
        {
            items.Add(item);
        }
    }

    private static IList<IMenuElement> GetItems(MenuFlyout flyout)
    {
        return flyout;
    }

    private static void Filter(IList<IMenuElement> items)
    {
        foreach (var item in items.OfType<MenuItem>().Where(item => !item.IsEnabled).ToList())
        {
            items.Remove(item);
        }
    }
}