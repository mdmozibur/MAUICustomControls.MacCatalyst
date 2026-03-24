using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace MAUICustomControls.MacCatalyst.Controls;

public abstract class FlyoutBase : BindableObject
{
    private WeakReference<PopoverButton>? _owner;

    public FlyoutPlacementMode Placement { get; set; } = FlyoutPlacementMode.Auto;

    public LightDismissOverlayMode LightDismissOverlayMode { get; set; } = LightDismissOverlayMode.Off;

    public event EventHandler<object>? Opening;

    public event EventHandler<object>? Opened;

    public event EventHandler<object>? Closed;

    internal void AttachOwner(PopoverButton? owner)
    {
        _owner = owner == null ? null : new WeakReference<PopoverButton>(owner);
    }

    internal void RaiseOpening() => Opening?.Invoke(this, EventArgs.Empty);

    internal void RaiseOpened() => Opened?.Invoke(this, EventArgs.Empty);

    internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    public virtual void Hide()
    {
        if (_owner != null && _owner.TryGetTarget(out var owner))
        {
            owner.HidePopover();
        }

        RaiseClosed();
    }

    public virtual void ShowAt(View target, FlyoutShowOptions? options = null)
    {
        _ = target;
        if (options != null)
        {
            Placement = options.Placement;
        }

        RaiseOpening();
        RaiseOpened();
    }

    internal abstract View? BuildView();
}

[ContentProperty(nameof(Content))]
public sealed class Flyout : FlyoutBase
{
    public static readonly BindableProperty AttachedFlyoutProperty = BindableProperty.CreateAttached(
        "AttachedFlyout",
        typeof(FlyoutBase),
        typeof(Flyout),
        null);

    public View? Content { get; set; }

    public static FlyoutBase? GetAttachedFlyout(BindableObject bindableObject)
    {
        return (FlyoutBase?)bindableObject.GetValue(AttachedFlyoutProperty);
    }

    public static void SetAttachedFlyout(BindableObject bindableObject, FlyoutBase? value)
    {
        bindableObject.SetValue(AttachedFlyoutProperty, value);
    }

    internal override View? BuildView() => Content;
}

[ContentProperty(nameof(Items))]
public class MenuFlyout : FlyoutBase
{
    public ObservableCollection<MenuFlyoutItemBase> Items { get; } = [];

    internal override View? BuildView()
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 2,
            Padding = 4,
        };

        foreach (var item in Items)
        {
            var view = item.BuildView();
            if (view != null)
            {
                stack.Children.Add(view);
            }
        }

        return stack;
    }
}

public abstract class MenuFlyoutItemBase : BindableObject
{
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(MenuFlyoutItemBase),
        14d);

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(View),
        typeof(MenuFlyoutItemBase),
        null);

    public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create(
        nameof(IsVisible),
        typeof(bool),
        typeof(MenuFlyoutItemBase),
        true);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(MenuFlyoutItemBase),
        null);

    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(
        nameof(IsEnabled),
        typeof(bool),
        typeof(MenuFlyoutItemBase),
        true);

    public static readonly BindableProperty StyleProperty = BindableProperty.Create(
        nameof(Style),
        typeof(Style),
        typeof(MenuFlyoutItemBase),
        null);

    public string? Name { get; set; }

    public object? DataContext { get; set; }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public View? Icon
    {
        get => (View?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public Style? Style
    {
        get => (Style?)GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    internal abstract View? BuildView();
}

public class MenuFlyoutItem : MenuFlyoutItemBase
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(MenuFlyoutItem),
        string.Empty);

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public object? Tag { get; set; }

    public event EventHandler? Clicked;

    protected virtual void OnClicked()
    {
        if (Command?.CanExecute(null) != false)
        {
            Command?.Execute(null);
        }

        Clicked?.Invoke(this, EventArgs.Empty);
    }

    internal override View? BuildView()
    {
        if (!IsVisible)
        {
            return null;
        }

        var header = new HorizontalStackLayout
        {
            BindingContext = BindingContext ?? DataContext,
            HorizontalOptions = LayoutOptions.Fill,
            Spacing = 6,
        };

        if (Icon != null)
        {
            Icon.BindingContext = header.BindingContext;
            header.Children.Add(Icon);
        }

        header.Children.Add(new Label
        {
            Text = Text,
            FontSize = FontSize,
            VerticalOptions = LayoutOptions.Center,
        });

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnClicked();
        header.GestureRecognizers.Add(tap);
        return header;
    }
}

public sealed class ToggleMenuFlyoutItem : MenuFlyoutItem
{
    public bool IsChecked { get; set; }

    internal override View? BuildView()
    {
        if (!IsVisible)
        {
            return null;
        }

        var checkbox = new CheckBox
        {
            BindingContext = BindingContext ?? DataContext,
            IsChecked = IsChecked,
        };

        var layout = new HorizontalStackLayout
        {
            Spacing = 6,
        };

        if (Icon != null)
        {
            Icon.BindingContext = checkbox.BindingContext;
            layout.Children.Add(Icon);
        }

        layout.Children.Add(checkbox);
        layout.Children.Add(new Label
        {
            Text = Text,
            FontSize = FontSize,
            VerticalOptions = LayoutOptions.Center,
        });

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            IsChecked = !IsChecked;
            checkbox.IsChecked = IsChecked;
            OnClicked();
        };
        layout.GestureRecognizers.Add(tap);
        return layout;
    }
}

[ContentProperty(nameof(Items))]
public sealed class MenuFlyoutSubItem : MenuFlyoutItemBase
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(MenuFlyoutSubItem),
        string.Empty);

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ObservableCollection<MenuFlyoutItemBase> Items { get; } = [];

    internal override View? BuildView()
    {
        if (!IsVisible)
        {
            return null;
        }

        var stack = new VerticalStackLayout
        {
            BindingContext = BindingContext ?? DataContext,
            Spacing = 2,
        };

        if (Icon != null)
        {
            Icon.BindingContext = stack.BindingContext;
            stack.Children.Add(Icon);
        }

        if (!string.IsNullOrWhiteSpace(Text))
        {
            stack.Children.Add(new Label
            {
                Text = Text,
                FontSize = FontSize,
                FontAttributes = FontAttributes.Bold,
            });
        }

        foreach (var item in Items)
        {
            var view = item.BuildView();
            if (view != null)
            {
                stack.Children.Add(view);
            }
        }

        return stack;
    }
}

public sealed class MenuFlyoutSeparator : MenuFlyoutItemBase
{
    internal override View? BuildView()
    {
        if (!IsVisible)
        {
            return null;
        }

        return new BoxView
        {
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.Fill,
            Color = Colors.Gray,
            Margin = new Thickness(0, 2),
        };
    }
}