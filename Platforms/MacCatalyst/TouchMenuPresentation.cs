#if IOS && !MACCATALYST
using CoreGraphics;
using Foundation;
using MAUICustomControls.MacCatalyst.Controls;
using Microsoft.Maui.Platform;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>
/// A menu opened from code on the iPad (UWP MenuFlyout.ShowAt): iPadOS has no public way to open a
/// UIMenu at a point, and its edit menu is a horizontal bar, so the menu is a popover list drawn like
/// an iPad menu. Separators start sections, the trailing text (UWP KeyboardAcceleratorTextOverride: a
/// shortcut, or "On") sits at the row's trailing edge, icons share one column, toggled items show a
/// check mark, disabled items are dimmed and hidden items are left out. Sub-menus are shown inline as
/// a titled section. Opening is raised before the menu is built and Closed after the chosen item.
/// </summary>
internal static class TouchMenuPresentation
{
    private const double RowHeight = 44;
    private const double MinWidth = 220;
    private const double MaxWidth = 360;

    private sealed record Row(string Title, string? Trailing, UIImage? Image, bool IsChecked, bool IsEnabled, IMenuElement Element);

    private sealed record Section(string? Title, List<Row> Rows);

    public static bool Show(IMenuFlyout flyout, UIView anchor, CGPoint location, IMauiContext mauiContext)
    {
        if (anchor.Window?.RootViewController is not { } root)
        {
            return false;
        }

        (flyout as ContextMenuFlyout)?.RaiseOpening();
        var sections = BuildSections(flyout, mauiContext, null);
        if (sections.Count == 0)
        {
            return true;
        }

        var presenter = root;
        while (presenter.PresentedViewController is { IsBeingDismissed: false } presented)
        {
            presenter = presented;
        }

        var controller = new MenuController(flyout, sections);
        controller.ModalPresentationStyle = UIModalPresentationStyle.Popover;
        if (controller.PopoverPresentationController is { } popover)
        {
            popover.SourceView = anchor;
            popover.SourceRect = new CGRect(location.X, location.Y, 1, 1);
            popover.PermittedArrowDirections = UIPopoverArrowDirection.Up | UIPopoverArrowDirection.Down;
            popover.Delegate = new AdaptAsPopover();
        }

        ContextMenuFlyout.OpenContextMenu = flyout;
        presenter.PresentViewController(controller, true, null);
        return true;
    }

    private static List<Section> BuildSections(IEnumerable<IMenuElement> items, IMauiContext mauiContext, string? title)
    {
        var sections = new List<Section>();
        var current = new Section(title, new List<Row>());
        foreach (var item in items)
        {
            if (MenuVisibility.IsHidden(item))
            {
                continue;
            }

            switch (item)
            {
                case IMenuFlyoutSeparator:
                    if (current.Rows.Count > 0)
                    {
                        sections.Add(current);
                    }

                    current = new Section(null, new List<Row>());
                    break;

                case IMenuFlyoutSubItem subItem:
                    if (current.Rows.Count > 0)
                    {
                        sections.Add(current);
                    }

                    sections.AddRange(BuildSections(subItem, mauiContext, subItem.Text));
                    current = new Section(null, new List<Row>());
                    break;

                default:
                    current.Rows.Add(new Row(
                        item.Text ?? string.Empty,
                        item is BindableObject bindable ? MenuItemAcceleratorText.GetText(bindable) : null,
                        ContextMenuSupport.LoadIcon(item.Source, mauiContext),
                        item is AppToggleMenuItem { IsChecked: true },
                        item.IsEnabled,
                        item));
                    break;
            }
        }

        if (current.Rows.Count > 0)
        {
            sections.Add(current);
        }

        return sections;
    }

    // Keeps the popover a popover in compact widths (Slide Over, narrow split view).
    private sealed class AdaptAsPopover : UIPopoverPresentationControllerDelegate
    {
        public override UIModalPresentationStyle GetAdaptivePresentationStyle(UIPresentationController controller, UITraitCollection traitCollection) =>
            UIModalPresentationStyle.None;
    }

    private sealed class MenuController : UITableViewController
    {
        private const string CellId = "menu";
        private readonly IMenuFlyout _flyout;
        private readonly List<Section> _sections;
        private readonly bool _hasIcons;
        private IMenuElement? _chosen;

        private static readonly UIImage BlankIcon = new UIGraphicsImageRenderer(new CGSize(24, 24)).CreateImage(_ => { });

        public MenuController(IMenuFlyout flyout, List<Section> sections)
            : base(UITableViewStyle.Plain)
        {
            _flyout = flyout;
            _sections = sections;
            _hasIcons = sections.Any(section => section.Rows.Any(row => row.Image is not null));
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            TableView.RegisterClassForCellReuse(typeof(UITableViewCell), CellId);
            // Rows size themselves (at least 44 pt) so text at any Dynamic Type size is not clipped.
            TableView.RowHeight = UITableView.AutomaticDimension;
            TableView.EstimatedRowHeight = (nfloat)RowHeight;
            TableView.SectionHeaderTopPadding = 0;
            TableView.AlwaysBounceVertical = false;
            TableView.SeparatorInset = new UIEdgeInsets(0, _hasIcons ? 52 : 16, 0, 0);
            TableView.BackgroundColor = UIColor.Clear;
            // iOS 26 blurs scroll content under the popover's edges; a menu that fits does not scroll.
            ScrollEdgeEffects.Hide(TableView);

            var rows = _sections.Sum(section => section.Rows.Count);
            var headers = _sections.Count(section => section.Title is not null);
            var height = rows * RowHeight + (_sections.Count - 1) * 8 + headers * 28;
            _contentHeight = height;
            PreferredContentSize = new CGSize(Width(), Math.Min(height, 640));
            TableView.ScrollEnabled = height > 640;
        }

        private double _contentHeight;

        // The popover's arrow side is safe-area inset: the popover grows by it, or the first row
        // starts half under the arrow and the list scrolls.
        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            var insets = TableView.AdjustedContentInset;
            var content = Math.Max(_contentHeight, (double)TableView.ContentSize.Height);
            var wanted = Math.Min(content + insets.Top + insets.Bottom, 640);
            TableView.ScrollEnabled = content + insets.Top + insets.Bottom > 640;
            if (Math.Abs(PreferredContentSize.Height - wanted) > 0.5)
            {
                PreferredContentSize = new CGSize(PreferredContentSize.Width, wanted);
            }

            // A menu that fits starts at its first row (not scrolled under the arrow).
            if (!TableView.ScrollEnabled && Math.Abs(TableView.ContentOffset.Y + insets.Top) > 0.5)
            {
                TableView.ContentOffset = new CGPoint(0, -insets.Top);
            }
        }

        private double Width()
        {
            var font = UIFont.PreferredBody;
            var widest = _sections.SelectMany(section => section.Rows).Select(row =>
                new NSString(row.Title).GetSizeUsingAttributes(new UIStringAttributes { Font = font }).Width +
                (row.Trailing is { Length: > 0 } trailing ? new NSString(trailing).GetSizeUsingAttributes(new UIStringAttributes { Font = font }).Width + 24 : 0) +
                (row.IsChecked ? 36 : 0)).DefaultIfEmpty(0).Max();
            return Math.Clamp(widest + (_hasIcons ? 52 : 16) + 24, MinWidth, MaxWidth);
        }

        public override nint NumberOfSections(UITableView tableView) => _sections.Count;

        public override nint RowsInSection(UITableView tableView, nint section) => _sections[(int)section].Rows.Count;

        public override string? TitleForHeader(UITableView tableView, nint section) => _sections[(int)section].Title;

        // The gap between sections stands for UWP's separator line.
        public override nfloat GetHeightForHeader(UITableView tableView, nint section) =>
            _sections[(int)section].Title is not null ? 28 : section == 0 ? 0 : 8;

        public override UIView? GetViewForHeader(UITableView tableView, nint section)
        {
            if (_sections[(int)section].Title is not null)
            {
                return null;
            }

            return new UIView { BackgroundColor = UIColor.Separator.ColorWithAlpha(0.12f) };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var row = _sections[indexPath.Section].Rows[indexPath.Row];
            var cell = tableView.DequeueReusableCell(CellId, indexPath);
            var content = UIListContentConfiguration.ValueCellConfiguration;
            content.Text = row.Title;
            content.SecondaryText = row.Trailing;
            // Rows without an icon keep the icon column, so every title starts at the same edge.
            content.Image = row.Image ?? (_hasIcons ? BlankIcon : null);
            content.ImageProperties.TintColor = UIColor.Label;
            content.ImageProperties.MaximumSize = new CGSize(22, 22);
            if (_hasIcons)
            {
                content.ImageProperties.ReservedLayoutSize = new CGSize(24, 24);
            }

            if (!row.IsEnabled)
            {
                content.TextProperties.Color = UIColor.TertiaryLabel;
                content.ImageProperties.TintColor = UIColor.TertiaryLabel;
            }

            content.DirectionalLayoutMargins = new NSDirectionalEdgeInsets(11, 16, 11, 16);
            cell.ContentConfiguration = content;
            // The gap between sections separates them; no line under a section's last row.
            var lastInSection = indexPath.Row == _sections[indexPath.Section].Rows.Count - 1;
            cell.SeparatorInset = lastInSection
                ? new UIEdgeInsets(0, (nfloat)MaxWidth, 0, 0)
                : new UIEdgeInsets(0, _hasIcons ? 52 : 16, 0, 0);
            cell.Accessory = row.IsChecked ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
            cell.UserInteractionEnabled = row.IsEnabled;
            cell.BackgroundColor = UIColor.Clear;
            cell.AccessibilityTraits = row.IsEnabled ? UIAccessibilityTrait.Button : UIAccessibilityTrait.Button | UIAccessibilityTrait.NotEnabled;
            return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            _chosen = _sections[indexPath.Section].Rows[indexPath.Row].Element;
            DismissViewController(true, null);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            if (ReferenceEquals(ContextMenuFlyout.OpenContextMenu, _flyout))
            {
                ContextMenuFlyout.OpenContextMenu = null;
            }

            // The chosen item runs once the menu is gone, then Closed, as in UWP.
            var chosen = _chosen;
            _chosen = null;
            if (chosen is not null)
            {
                chosen.Clicked();
            }

            (_flyout as ContextMenuFlyout)?.RaiseClosed();
        }
    }
}
#endif
