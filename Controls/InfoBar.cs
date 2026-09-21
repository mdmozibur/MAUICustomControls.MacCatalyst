using Microsoft.Maui.Controls.Shapes;

namespace MAUICustomControls.MacCatalyst.Controls;

public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

public enum InfoBarCloseReason
{
    CloseButton,
    Programmatic,
}

public sealed class InfoBarClosingEventArgs : EventArgs
{
    public InfoBarClosingEventArgs(InfoBarCloseReason reason) => Reason = reason;

    public InfoBarCloseReason Reason { get; }

    public bool Cancel { get; set; }
}

public sealed class InfoBarClosedEventArgs : EventArgs
{
    public InfoBarClosedEventArgs(InfoBarCloseReason reason) => Reason = reason;

    public InfoBarCloseReason Reason { get; }
}

/// <summary>
/// WinUI's InfoBar: a severity-tinted bar with an icon, title, message, optional action and close
/// button. Colours follow WinUI's light and dark palettes; the icons are SF Symbols. Opening it
/// announces the title and message to VoiceOver. <see cref="View.IsVisible"/> (XAML Visibility) and
/// <see cref="IsOpen"/> are independent, as in UWP: the bar shows when both are true.
/// </summary>
public sealed class InfoBar : ContentView
{
    private readonly Border _container;
    private readonly Image _icon;
    private readonly Label _titleLabel;
    private readonly Label _messageLabel;
    private readonly ContentView _actionHost;
    private readonly ImageButton _closeButton;
    private InfoBarCloseReason? _closeReason;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), typeof(bool), typeof(InfoBar), false, propertyChanged: OnIsOpenChanged);

    public static readonly BindableProperty SeverityProperty = BindableProperty.Create(
        nameof(Severity), typeof(InfoBarSeverity), typeof(InfoBar), InfoBarSeverity.Informational, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(InfoBar), string.Empty, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message), typeof(string), typeof(InfoBar), string.Empty, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IsClosableProperty = BindableProperty.Create(
        nameof(IsClosable), typeof(bool), typeof(InfoBar), true, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IsIconVisibleProperty = BindableProperty.Create(
        nameof(IsIconVisible), typeof(bool), typeof(InfoBar), true, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty ActionButtonProperty = BindableProperty.Create(
        nameof(ActionButton), typeof(View), typeof(InfoBar), null, propertyChanged: OnActionButtonChanged);

    public event EventHandler<InfoBarClosingEventArgs>? Closing;
    public event EventHandler<InfoBarClosedEventArgs>? Closed;
    public event EventHandler? CloseButtonClick;

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public InfoBarSeverity Severity
    {
        get => (InfoBarSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public bool IsIconVisible
    {
        get => (bool)GetValue(IsIconVisibleProperty);
        set => SetValue(IsIconVisibleProperty, value);
    }

    public View? ActionButton
    {
        get => (View?)GetValue(ActionButtonProperty);
        set => SetValue(ActionButtonProperty, value);
    }

    public InfoBar()
    {
        _icon = new Image
        {
            WidthRequest = 16,
            HeightRequest = 16,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 1, 0, 0),
        };
        AutomationProperties.SetIsInAccessibleTree(_icon, false);

        _titleLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            IsVisible = false,
            LineBreakMode = LineBreakMode.WordWrap,
        };

        _messageLabel = new Label
        {
            IsVisible = false,
            LineBreakMode = LineBreakMode.WordWrap,
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { _titleLabel, _messageLabel },
        };

        _actionHost = new ContentView
        {
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
        };

        _closeButton = new ImageButton
        {
            Source = SymbolImage("xmark", 11),
            Padding = new Thickness(6),
            BackgroundColor = Colors.Transparent,
            WidthRequest = 24,
            HeightRequest = 24,
            CornerRadius = 4,
            VerticalOptions = LayoutOptions.Start,
            IsVisible = false,
        };
        SemanticProperties.SetDescription(_closeButton, "Close");
        ToolTipProperties.SetText(_closeButton, "Close");
        _closeButton.Clicked += (_, _) =>
        {
            CloseButtonClick?.Invoke(this, EventArgs.Empty);
            RequestClose(InfoBarCloseReason.CloseButton);
        };

        var layout = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        Grid.SetColumn(_icon, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(_actionHost, 2);
        Grid.SetColumn(_closeButton, 3);

        layout.Children.Add(_icon);
        layout.Children.Add(textStack);
        layout.Children.Add(_actionHost);
        layout.Children.Add(_closeButton);

        // WinUI's template draws the bar at the top of the control (ContentRoot is top-aligned), so
        // a bar left at the default Stretch alignment sits at the top of its layout cell instead of
        // filling it. The control itself defaults to the top as well: the empty rest of an open
        // bar's cell would otherwise swallow the clicks meant for the controls under it (UWP's
        // empty template area is not hit-testable).
        VerticalOptions = LayoutOptions.Start;
        _container = new Border
        {
            VerticalOptions = LayoutOptions.Start,
            Padding = new Thickness(12, 10),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) },
            Content = layout,
            IsVisible = false,
        };

        base.Content = _container;
        UpdateAppearance();

        // The icons are rendered images; redraw them in the other theme's colour. Subscribed only
        // while loaded, so the application does not keep closed bars alive.
        Loaded += (_, _) =>
        {
            if (Application.Current is { } application)
            {
                application.RequestedThemeChanged -= OnRequestedThemeChanged;
                application.RequestedThemeChanged += OnRequestedThemeChanged;
                UpdateAppearance();
            }
        };
        Unloaded += (_, _) =>
        {
            if (Application.Current is { } application)
            {
                application.RequestedThemeChanged -= OnRequestedThemeChanged;
            }
        };
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => UpdateAppearance();

    private void RequestClose(InfoBarCloseReason reason)
    {
        var closing = new InfoBarClosingEventArgs(reason);
        Closing?.Invoke(this, closing);
        if (closing.Cancel)
        {
            return;
        }

        _closeReason = reason;
        IsOpen = false;
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var infoBar = (InfoBar)bindable;
        infoBar.UpdateAppearance();

        if (newValue is true)
        {
            // An InfoBar reports something that happened; VoiceOver users would otherwise miss it.
            var announcement = string.Join(". ", new[] { infoBar.Title, infoBar.Message }.Where(text => !string.IsNullOrWhiteSpace(text)));
            if (announcement.Length > 0)
            {
                SemanticScreenReader.Announce(announcement);
            }

            return;
        }

        if (oldValue is true)
        {
            var reason = infoBar._closeReason ?? InfoBarCloseReason.Programmatic;
            infoBar._closeReason = null;
            infoBar.Closed?.Invoke(infoBar, new InfoBarClosedEventArgs(reason));
        }
    }

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is InfoBar infoBar)
        {
            infoBar.UpdateAppearance();
        }
    }

    private static void OnActionButtonChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not InfoBar infoBar)
        {
            return;
        }

        infoBar._actionHost.Content = newValue as View;
        infoBar._actionHost.IsVisible = newValue is View;
    }

    private void UpdateAppearance()
    {
        // Closed, UWP collapses the bar's template: it takes no room and no input, while the element
        // itself stays Visible. The container is hidden, so the bar measures to nothing; but a Fill
        // alignment still arranges it over its whole layout cell, where it would swallow clicks meant
        // for the controls sharing that cell. So it also stops taking input while closed.
        _container.IsVisible = IsOpen;
        InputTransparent = !IsOpen;
        _titleLabel.Text = Title;
        _titleLabel.IsVisible = !string.IsNullOrWhiteSpace(Title);
        _messageLabel.Text = Message;
        _messageLabel.IsVisible = !string.IsNullOrWhiteSpace(Message);
        _closeButton.IsVisible = IsClosable;

        // WinUI's InfoBar palette (background, icon) for light and dark themes.
        var (lightBackground, darkBackground, lightIcon, darkIcon, symbol) = Severity switch
        {
            InfoBarSeverity.Success => ("#DFF6DD", "#393D1B", "#0F7B0F", "#6CCB5F", "checkmark.circle.fill"),
            InfoBarSeverity.Warning => ("#FFF4CE", "#433519", "#9D5D00", "#FCE100", "exclamationmark.triangle.fill"),
            InfoBarSeverity.Error => ("#FDE7E9", "#442726", "#C42B1C", "#FF99A4", "xmark.octagon.fill"),
            _ => ("#F6F6F6", "#2B2B2B", "#005FB8", "#60CDFF", "info.circle.fill"),
        };

        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        _container.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb(lightBackground), Color.FromArgb(darkBackground));
        _container.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(Color.FromArgb("#0F000000")),
            new SolidColorBrush(Color.FromArgb("#19000000")));

        _icon.Source = SymbolImage(symbol, 15, Color.FromArgb(dark ? darkIcon : lightIcon));
        _icon.IsVisible = IsIconVisible;
        _closeButton.Source = SymbolImage("xmark", 11, dark ? Colors.White : Colors.Black);

        var severityName = Severity switch
        {
            InfoBarSeverity.Success => "Success",
            InfoBarSeverity.Warning => "Warning",
            InfoBarSeverity.Error => "Error",
            _ => "Information",
        };
        SemanticProperties.SetDescription(_container, string.Join(". ", new[] { severityName, Title, Message }.Where(text => !string.IsNullOrWhiteSpace(text))));
    }

    /// <summary>An SF Symbol as an image source (the bar's icons and close glyph).</summary>
    private static ImageSource? SymbolImage(string name, double pointSize, Color? color = null)
    {
#if MACCATALYST || IOS
        var configuration = UIKit.UIImageSymbolConfiguration.Create((nfloat)pointSize, UIKit.UIImageSymbolWeight.Medium);
        var image = UIKit.UIImage.GetSystemImage(name, configuration);
        if (image is null)
        {
            return null;
        }

        if (color is not null)
        {
            image = image.ApplyTintColor(Microsoft.Maui.Platform.ColorExtensions.ToPlatform(color), UIKit.UIImageRenderingMode.AlwaysOriginal);
        }

        var data = image.AsPNG();
        if (data is null)
        {
            return null;
        }

        var bytes = data.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(bytes));
#else
        _ = name;
        _ = pointSize;
        _ = color;
        return null;
#endif
    }
}
