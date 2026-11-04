namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Maui;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MauiView = Microsoft.Maui.IView;

public sealed record DialogPresentationOptions
{
    public double MinWidth { get; init; } = 320;
    public double MaxWidth { get; init; } = 548;
    public double MinHeight { get; init; }
    public double MaxHeight { get; init; } = 756;

    /// <summary>Enter outside a multi-line text field (the default button, as in WinUI).</summary>
    public Action? OnEnter { get; init; }

    /// <summary>Escape (closes the dialog, as in WinUI).</summary>
    public Action? OnEscape { get; init; }

    public string? AccessibilityLabel { get; init; }
}

/// <summary>
/// A modal dialog on Linux, as the Mac's DialogPresentation is: the MAUI view in a card centred over
/// the dimmed window, within the dialog's size limits, on the window's overlay layer. It takes the
/// pointer and keys from everything beneath it while open.
/// </summary>
public sealed class DialogPresentation
{
    private const double WindowMargin = 24;
    private static int _openCount;
    private static readonly List<DialogPresentation> Presentations = new();

    private readonly Window _window;
    private readonly OverlayLayer _layer;
    private readonly Panel _scrim;
    private readonly Border _card;
    private readonly DialogPresentationOptions _options;
    private readonly MauiView _content;
    private IInputElement? _previousFocus;
    private bool _dismissed;

    private DialogPresentation(Window window, OverlayLayer layer, Control content, MauiView model, DialogPresentationOptions options)
    {
        _window = window;
        _layer = layer;
        _options = options;
        _content = model;
        var dark = window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

        _card = new Border
        {
            Child = content,
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x2B, 0x2B, 0x2B) : Color.FromRgb(0xF9, 0xF9, 0xF9)),
            BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x26, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            BoxShadow = BoxShadows.Parse("0 8 32 0 #40000000"),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(_card, options.AccessibilityLabel);
        KeyboardNavigation.SetTabNavigation(_card, Avalonia.Input.KeyboardNavigationMode.Cycle);

        // Covers the window: dims it and swallows the pointer, so nothing beneath can be used.
        _scrim = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Children = { _card },
        };
        _scrim.PointerPressed += (_, e) => e.Handled = true;
        _scrim.PointerReleased += (_, e) => e.Handled = true;
        _scrim.PointerWheelChanged += (_, e) => e.Handled = true;
    }

    public static bool IsAnyOpen => Volatile.Read(ref _openCount) > 0;
    public static Microsoft.Maui.Controls.Element? ActiveContent => Presentations.LastOrDefault()?._content as Microsoft.Maui.Controls.Element;
    public static bool IsWithinActiveDialog(Microsoft.Maui.Controls.Element? element)
    {
        var root = ActiveContent;
        if (root is null) return true;
        for (; element is not null; element = element.Parent)
            if (ReferenceEquals(element, root)) return true;
        return false;
    }

    public event Action? Dismissed;

    public static DialogPresentation? Present(MauiView content, Microsoft.Maui.IMauiContext mauiContext, DialogPresentationOptions options)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ||
            OverlayLayer.GetOverlayLayer(window) is not { } layer ||
            content.ToPlatform(mauiContext) is not Control platformView)
        {
            return null;
        }

        var presentation = new DialogPresentation(window, layer, platformView, content, options);
        presentation.Show();
        return presentation;
    }

    /// <summary>Re-fits the card after the content or the window changed size.</summary>
    public void InvalidateSize()
    {
        var bounds = _window.ClientSize;
        var maxWidth = Math.Max(0, Math.Min(_options.MaxWidth, bounds.Width - 2 * WindowMargin));
        var maxHeight = Math.Max(0, Math.Min(_options.MaxHeight, bounds.Height - 2 * WindowMargin));
        _card.MinWidth = Math.Min(_options.MinWidth, maxWidth);
        _card.MaxWidth = maxWidth;
        _card.MinHeight = Math.Min(_options.MinHeight, maxHeight);
        _card.MaxHeight = maxHeight;
        _scrim.Width = bounds.Width;
        _scrim.Height = bounds.Height;
        _card.InvalidateMeasure();
    }

    public void Dismiss()
    {
        if (_dismissed)
        {
            return;
        }

        _dismissed = true;
        Presentations.Remove(this);
        _window.RemoveHandler(InputElement.KeyDownEvent, OnWindowKeyDown);
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _layer.Children.Remove(_scrim);
        _card.Child = null;
        Interlocked.Decrement(ref _openCount);
        _previousFocus?.Focus();
        Dismissed?.Invoke();
    }

    private void Show()
    {
        Interlocked.Increment(ref _openCount);
        Presentations.Add(this);
        _previousFocus = TopLevel.GetTopLevel(_window)?.FocusManager?.GetFocusedElement();
        InvalidateSize();
        _layer.Children.Add(_scrim);

        // Focused controls get first refusal (a multiline editor or an open
        // dropdown owns Enter). The compatibility key route stops at our model
        // root, so the drawing page cannot consume these keys first.
        _window.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Bubble);
        _window.PropertyChanged += OnWindowPropertyChanged;

        // The first text field takes the keyboard, else the dialog itself, as on the Mac.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (FindFirst<TextBox>(_card) is { IsEnabled: true } text)
            {
                text.Focus();
            }
            else
            {
                _card.Focusable = true;
                _card.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty)
        {
            InvalidateSize();
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Only the topmost dialog answers.
        if (_dismissed || !ReferenceEquals(_layer.Children.LastOrDefault(child => child is Panel), _scrim))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                _options.OnEscape?.Invoke();
                break;
            case Key.Enter when e.Source is not TextBox { AcceptsReturn: true }:
                e.Handled = true;
                _options.OnEnter?.Invoke();
                break;
        }
    }

    private static T? FindFirst<T>(Visual root) where T : Visual
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(root))
        {
            if (child is T match)
            {
                return match;
            }

            if (FindFirst<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
