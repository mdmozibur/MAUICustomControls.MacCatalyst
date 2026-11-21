namespace MAUICustomControls.MacCatalyst.Controls;

[Microsoft.Maui.Controls.ContentProperty(nameof(Body))]
public sealed class Expander : ContentView
{
    private readonly Grid _rootGrid;
    private readonly ContentView _headerHost;
    private readonly ContentView _contentHost;
    private readonly GraphicsView _chevron;
    private readonly AccessibleItemView _headerItem;

    // typeof(object), not typeof(View): UWP's Expander.Header is an object and is most often
    // set to a plain string, which has no conversion to View. Strings are wrapped in a Label
    // below. An object-typed property is also what the localization shim can write to.
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(
        nameof(Header), typeof(object), typeof(Expander), null, propertyChanged: OnHeaderChanged);

    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(Expander), false, BindingMode.TwoWay, propertyChanged: OnIsExpandedChanged);

    // XAML assigns through the bindable property rather than the CLR setter.
    // Keep its body separate from ContentView.Content, which owns the header and body hosts.
    public static readonly BindableProperty BodyProperty = BindableProperty.Create(
        nameof(Body), typeof(View), typeof(Expander), null,
        propertyChanged: (bindable, _, value) => ((Expander)bindable)._contentHost.Content = (View?)value);

    // Preserve the code-built Content API without invoking ContentView's handler
    // mapper, which would detach the root and drop focus on every body update.
    public new static readonly BindableProperty ContentProperty = BodyProperty;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public event EventHandler<ExpandedChangedEventArgs>? ExpandedChanged;

    public Expander()
    {
        _chevron = new GraphicsView
        {
            Drawable = new ChevronDrawable(),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 20,
            HeightRequest = 12,
            Rotation = 0,
        };
        // Decorative: the header item reports the state.
        AutomationProperties.SetExcludedWithChildren(_chevron, true);

        _headerHost = new ContentView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
        };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            Padding = new Thickness(4),
        };

        Grid.SetColumn(_chevron, 0);
        Grid.SetColumn(_headerHost, 1);
        headerRow.Children.Add(_chevron);
        headerRow.Children.Add(_headerHost);

        // The header is a disclosure button to the keyboard and VoiceOver: Space/Return toggle it,
        // → expands and ← collapses, and VoiceOver reads it as expanded or collapsed.
        _headerItem = new AccessibleItemView
        {
            Role = AccessibleItemRole.Disclosure,
            Content = headerRow,
            BackgroundColor = Colors.Transparent,
        };
        _headerItem.Invoked += (_, _) => IsExpanded = !IsExpanded;

        var headerTap = new TapGestureRecognizer();
        headerTap.Tapped += (_, _) => IsExpanded = !IsExpanded;
        _headerItem.GestureRecognizers.Add(headerTap);

        _contentHost = new ContentView
        {
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill,
        };

        _rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            HorizontalOptions = LayoutOptions.Fill,
        };

        Grid.SetRow(_headerItem, 0);
        Grid.SetRow(_contentHost, 1);
        _rootGrid.Children.Add(_headerItem);
        _rootGrid.Children.Add(_contentHost);

        base.Content = _rootGrid;
    }

    private static void OnHeaderChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Expander expander)
            return;

        expander._headerHost.Content = newValue switch
        {
            View view => view,
            null => null,
            _ => new Label
            {
                Text = newValue.ToString(),
                VerticalTextAlignment = TextAlignment.Center,
            },
        };
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Expander expander)
        {
            var expanded = (bool)newValue;
            expander._contentHost.IsVisible = expanded;
            expander._headerItem.IsSelected = expanded;
            expander._chevron.RotateTo(expanded ? 90 : 0, 150, Easing.CubicInOut);
            expander.ExpandedChanged?.Invoke(expander, new ExpandedChangedEventArgs(expanded));
        }
    }

    /// <summary>
    /// The expandable body content. In XAML this is the default content property.
    /// </summary>
    public View? Body
    {
        get => (View?)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public new View? Content
    {
        get => Body;
        set => Body = value;
    }

    // A vector disclosure mark also works when the app's text font has no chevron glyph.
    private sealed class ChevronDrawable : Microsoft.Maui.Graphics.IDrawable
    {
        public void Draw(Microsoft.Maui.Graphics.ICanvas canvas, Microsoft.Maui.Graphics.RectF dirtyRect)
        {
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(7, 2, 12, 6);
            canvas.DrawLine(12, 6, 7, 10);
        }
    }
}

public sealed class ExpandedChangedEventArgs : EventArgs
{
    public bool IsExpanded { get; }

    public ExpandedChangedEventArgs(bool isExpanded)
    {
        IsExpanded = isExpanded;
    }
}
