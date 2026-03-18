using Microsoft.Maui.Controls.Shapes;

namespace MAUICustomControls.MacCatalyst.Controls;

public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

public sealed class InfoBar : ContentView
{
    private readonly Border _container;
    private readonly Label _iconLabel;
    private readonly Label _titleLabel;
    private readonly Label _messageLabel;
    private readonly ContentView _actionHost;
    private readonly Button _closeButton;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(InfoBar),
        false,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty SeverityProperty = BindableProperty.Create(
        nameof(Severity),
        typeof(InfoBarSeverity),
        typeof(InfoBar),
        InfoBarSeverity.Informational,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(InfoBar),
        string.Empty,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message),
        typeof(string),
        typeof(InfoBar),
        string.Empty,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IsClosableProperty = BindableProperty.Create(
        nameof(IsClosable),
        typeof(bool),
        typeof(InfoBar),
        true,
        propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty ActionButtonProperty = BindableProperty.Create(
        nameof(ActionButton),
        typeof(View),
        typeof(InfoBar),
        null,
        propertyChanged: OnActionButtonChanged);

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

    public View? ActionButton
    {
        get => (View?)GetValue(ActionButtonProperty);
        set => SetValue(ActionButtonProperty, value);
    }

    public InfoBar()
    {
        _iconLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            VerticalOptions = LayoutOptions.Start,
        };

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
            Spacing = 4,
            Children = { _titleLabel, _messageLabel },
        };

        _actionHost = new ContentView
        {
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
        };

        _closeButton = new Button
        {
            Text = "x",
            Padding = new Thickness(4, 0),
            BackgroundColor = Colors.Transparent,
            WidthRequest = 32,
            HeightRequest = 32,
            IsVisible = false,
        };
        _closeButton.Clicked += (_, _) => IsOpen = false;

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

        Grid.SetColumn(_iconLabel, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(_actionHost, 2);
        Grid.SetColumn(_closeButton, 3);

        layout.Children.Add(_iconLabel);
        layout.Children.Add(textStack);
        layout.Children.Add(_actionHost);
        layout.Children.Add(_closeButton);

        _container = new Border
        {
            Padding = new Thickness(12),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Content = layout,
        };

        base.Content = _container;
        UpdateAppearance();
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
        IsVisible = IsOpen;
        _titleLabel.Text = Title;
        _titleLabel.IsVisible = !string.IsNullOrWhiteSpace(Title);
        _messageLabel.Text = Message;
        _messageLabel.IsVisible = !string.IsNullOrWhiteSpace(Message);
        _closeButton.IsVisible = IsClosable;

        var (background, stroke, foreground, glyph) = Severity switch
        {
            InfoBarSeverity.Success => (Color.FromArgb("#E8F7ED"), Color.FromArgb("#7BC47F"), Color.FromArgb("#1F6B2C"), "OK"),
            InfoBarSeverity.Warning => (Color.FromArgb("#FFF5E6"), Color.FromArgb("#F2B366"), Color.FromArgb("#8A5700"), "!"),
            InfoBarSeverity.Error => (Color.FromArgb("#FDECEC"), Color.FromArgb("#E17D7D"), Color.FromArgb("#8E2C2C"), "!"),
            _ => (Color.FromArgb("#EAF3FF"), Color.FromArgb("#7BAFE6"), Color.FromArgb("#184E8C"), "i"),
        };

        _container.Background = new SolidColorBrush(background);
        _container.Stroke = new SolidColorBrush(stroke);
        _iconLabel.Text = glyph;
        _iconLabel.TextColor = foreground;
        _titleLabel.TextColor = foreground;
        _messageLabel.TextColor = foreground;
        _closeButton.TextColor = foreground;
    }
}
