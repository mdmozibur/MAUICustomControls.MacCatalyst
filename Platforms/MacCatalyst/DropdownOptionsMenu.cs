using CoreGraphics;
using Foundation;
using Microsoft.Maui;
using MAUICustomControls.MacCatalyst.Controls;
using ObjCRuntime;
using UIKit;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

/// <summary>One entry of a <see cref="DropdownOptionsMenu"/>.</summary>
internal readonly record struct DropdownMenuItem(string Title, UIImage? Image, bool IsSelected);

/// <summary>
/// The option list of a ToggleDropdown, shown in the app's popover. UIButton's own menu becomes an
/// AppKit pop-up button menu on the Mac, which drops the item icons and opens only after AppKit's
/// two-second "stuck gesture" wait; this opens at once and shows each option's icon, like UWP's
/// MenuFlyout. Rows follow macOS menus: checkmark column, icon, title, accent highlight on hover.
/// </summary>
internal sealed class DropdownOptionsMenu
{
    private static DropdownOptionsMenu? _open;

    private readonly PopoverHostViewController _controller;
    private readonly PopoverTransitioningDelegate _transitioning;
    private readonly Action<int> _chosen;
    private bool _dismissed;

    private DropdownOptionsMenu(UIView source, IReadOnlyList<DropdownMenuItem> items, UIColor accent, Action<int> chosen)
    {
        _chosen = chosen;
        var list = new OptionListView(items, accent, Choose);
        _controller = new PopoverHostViewController(list, () => Dismiss(null));
        _controller.PreferredContentSize = new CGSize(
            list.ContentSize.Width + 2 * OptionListView.Inset,
            list.ContentSize.Height + 2 * OptionListView.Inset);
        _transitioning = new PopoverTransitioningDelegate(
            source,
            PopoverDirection.Down,
            PopoverAlignment.Start,
            new PopoverChromeOptions(8, false, new Thickness(OptionListView.Inset)),
            () => Dismiss(null));
        _controller.ModalPresentationStyle = UIModalPresentationStyle.Custom;
        _controller.TransitioningDelegate = _transitioning;
    }

    public static bool IsOpen => _open is not null;

    /// <summary>Shows the options below <paramref name="source"/>; <paramref name="chosen"/> gets the picked index.</summary>
    public static void Show(UIView source, IReadOnlyList<DropdownMenuItem> items, UIColor accent, Action<int> chosen)
    {
        _open?.Dismiss(null);
        if (items.Count == 0 || PresentationHelpers.GetTopViewController() is not { } presenter)
        {
            return;
        }

        var menu = new DropdownOptionsMenu(source, items, accent, chosen);
        _open = menu;
        presenter.PresentViewController(menu._controller, true, () => (menu._controller.View as OptionListView)?.BecomeFirstResponder());
    }

    /// <summary>Closes the open menu, if any (its dropdown went away).</summary>
    public static void DismissOpen() => _open?.Dismiss(null);

    private void Choose(int index) => Dismiss(index);

    private void Dismiss(int? chosenIndex)
    {
        if (_dismissed)
        {
            return;
        }

        _dismissed = true;
        if (ReferenceEquals(_open, this))
        {
            _open = null;
        }

        _controller.DismissViewController(true, () =>
        {
            GC.KeepAlive(_transitioning);
            if (chosenIndex is { } index)
            {
                _chosen(index);
            }
        });
    }

    /// <summary>The rows, plus mouse and keyboard handling.</summary>
    private sealed class OptionListView : UIView
    {
        public const double Inset = 5;
        private const double RowHeight = 24;
        private const double CheckColumn = 22;
        private const double IconSize = 16;
        private const double IconGap = 7;
        private const double TrailingPadding = 14;

        private readonly List<OptionRow> _rows = new();
        private readonly Action<int> _choose;
        private int _highlighted = -1;

        public OptionListView(IReadOnlyList<DropdownMenuItem> items, UIColor accent, Action<int> choose)
        {
            _choose = choose;
            BackgroundColor = UIColor.Clear;
            AccessibilityTraits = UIAccessibilityTrait.None;

            var font = UIFont.SystemFontOfSize(13);
            var hasIcons = items.Any(item => item.Image is not null);
            double widest = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var index = i;
                var row = new OptionRow(items[i], font, accent, hasIcons, () => _choose(index), hovered => Highlight(hovered ? index : -1));
                _rows.Add(row);
                AddSubview(row);
                widest = Math.Max(widest, row.PreferredWidth);
            }

            ContentSize = new CGSize(Math.Ceiling(widest), RowHeight * items.Count);
            _highlighted = -1;
        }

        public CGSize ContentSize { get; }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].Frame = new CGRect(0, i * RowHeight, Bounds.Width, RowHeight);
            }
        }

        public override bool CanBecomeFirstResponder => true;

        public override UIKeyCommand[] KeyCommands =>
        [
            UIKeyCommand.Create(UIKeyCommand.UpArrow, 0, new Selector("menuUp:")),
            UIKeyCommand.Create(UIKeyCommand.DownArrow, 0, new Selector("menuDown:")),
            UIKeyCommand.Create((NSString)"\r", 0, new Selector("menuChoose:")),
            UIKeyCommand.Create((NSString)" ", 0, new Selector("menuChoose:")),
        ];

        [Export("menuUp:")]
        public void MenuUp(UIKeyCommand command) => Highlight(_highlighted <= 0 ? _rows.Count - 1 : _highlighted - 1);

        [Export("menuDown:")]
        public void MenuDown(UIKeyCommand command) => Highlight(_highlighted >= _rows.Count - 1 ? 0 : _highlighted + 1);

        [Export("menuChoose:")]
        public void MenuChoose(UIKeyCommand command)
        {
            if (_highlighted >= 0)
            {
                _choose(_highlighted);
            }
        }

        private void Highlight(int index)
        {
            _highlighted = index;
            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].SetHighlighted(i == index);
            }
        }

        private sealed class OptionRow : UIControl
        {
            private readonly UILabel _title;
            private readonly UIImageView _icon;
            private readonly UIImageView _check;
            private readonly UIView _highlight;
            private readonly UIColor _accent;
            private readonly bool _reserveIcon;

            public OptionRow(DropdownMenuItem item, UIFont font, UIColor accent, bool reserveIcon, Action chosen, Action<bool> hovered)
            {
                _accent = accent;
                _reserveIcon = reserveIcon;

                _highlight = new UIView { UserInteractionEnabled = false, BackgroundColor = UIColor.Clear };
                _highlight.Layer.CornerRadius = 4;
                AddSubview(_highlight);

                _check = new UIImageView(UIImage.GetSystemImage("checkmark", UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(11, UIFontWeight.Semibold))))
                {
                    ContentMode = UIViewContentMode.Center,
                    Hidden = !item.IsSelected,
                    TintColor = UIColor.Label,
                };
                AddSubview(_check);

                _icon = new UIImageView(item.Image?.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate))
                {
                    ContentMode = UIViewContentMode.Center,
                    TintColor = UIColor.Label,
                };
                AddSubview(_icon);

                _title = new UILabel { Text = item.Title, Font = font, TextColor = UIColor.Label };
                AddSubview(_title);

                var titleWidth = new NSString(item.Title).GetSizeUsingAttributes(new UIStringAttributes { Font = font }).Width;
                PreferredWidth = CheckColumn + (reserveIcon ? IconSize + IconGap : 0) + (double)titleWidth + TrailingPadding;

                AddTarget((_, _) => chosen(), UIControlEvent.TouchUpInside);
                AddGestureRecognizer(new UIHoverGestureRecognizer(recognizer =>
                    hovered(recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed)));

                IsAccessibilityElement = true;
                AccessibilityLabel = item.Title;
                AccessibilityTraits = item.IsSelected ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected : UIAccessibilityTrait.Button;
            }

            public double PreferredWidth { get; }

            public override void LayoutSubviews()
            {
                base.LayoutSubviews();
                var height = Bounds.Height;
                _highlight.Frame = Bounds;
                _check.Frame = new CGRect(0, 0, CheckColumn, height);
                var x = CheckColumn;
                if (_reserveIcon)
                {
                    _icon.Frame = new CGRect(x, (height - IconSize) / 2, IconSize, IconSize);
                    x += IconSize + IconGap;
                }

                _title.Frame = new CGRect(x, 0, Math.Max(0, Bounds.Width - x - TrailingPadding / 2), height);
            }

            public void SetHighlighted(bool highlighted)
            {
                // macOS menus: accent background with white content under the pointer.
                var foreground = highlighted ? UIColor.White : UIColor.Label;
                _highlight.BackgroundColor = highlighted ? _accent : UIColor.Clear;
                _title.TextColor = foreground;
                _icon.TintColor = foreground;
                _check.TintColor = foreground;
            }
        }
    }
}
