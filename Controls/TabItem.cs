using System;
using System.IO;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class TabItem : TemplatedView
{
    private Grid? _rootGrid;
    private BoxView? _selectionHighlighter;
    private Label? _titleLabel;
    private Button? _closeButton;
    private BoxView? _separator;
    private bool _isPointerOver;

    internal event EventHandler? SelectionRequested;

    internal event EventHandler? CloseRequested;

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(TabItem), false, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TabItem), null, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty IsClosableProperty =
        BindableProperty.Create(nameof(IsClosable), typeof(bool), typeof(TabItem), true, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty SelectionBarHeightProperty =
        BindableProperty.Create(nameof(SelectionBarHeight), typeof(double), typeof(TabItem), 2d, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty UnsavedDotVisiblityProperty =
        BindableProperty.Create(nameof(UnsavedDotVisiblity), typeof(bool), typeof(TabItem), true, propertyChanged: OnVisualPropertyChanged);

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public double SelectionBarHeight
    {
        get => (double)GetValue(SelectionBarHeightProperty);
        set => SetValue(SelectionBarHeightProperty, value);
    }

    public bool UnsavedDotVisiblity
    {
        get => (bool)GetValue(UnsavedDotVisiblityProperty);
        set => SetValue(UnsavedDotVisiblityProperty, value);
    }

    public TabItem()
    {
        if (Application.Current?.Resources.TryGetValue("TabItemControlTemplate", out var template) == true
            && template is ControlTemplate ct)
        {
            ControlTemplate = ct;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_closeButton is not null) _closeButton.Clicked -= CloseButton_Clicked;

        _rootGrid = GetTemplateChild("RootGrid") as Grid;
        _selectionHighlighter = GetTemplateChild("SelectionHighlighter") as BoxView;
        _titleLabel = GetTemplateChild("TitleLabel") as Label;
        _closeButton = GetTemplateChild("CloseButton") as Button;
        _separator = GetTemplateChild("Separator") as BoxView;

        if (_closeButton is not null) _closeButton.Clicked += CloseButton_Clicked;

        if (_rootGrid is not null)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += Root_Tapped;
            _rootGrid.GestureRecognizers.Add(tapGesture);

            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += Pointer_Entered;
            pointerGesture.PointerExited += Pointer_Exited;
            _rootGrid.GestureRecognizers.Add(pointerGesture);
        }

        UpdateVisualState();
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((TabItem)bindable).UpdateVisualState();
    }

    private void Root_Tapped(object? sender, TappedEventArgs e)
    {
        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Clicked(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, e);
    }

    private void Pointer_Entered(object? sender, PointerEventArgs e)
    {
        _isPointerOver = true;
        UpdateVisualState();
    }

    private void Pointer_Exited(object? sender, PointerEventArgs e)
    {
        _isPointerOver = false;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_rootGrid is not null)
            _rootGrid.RowDefinitions[0].Height = new GridLength(SelectionBarHeight);

        if (_titleLabel is not null)
        {
            _titleLabel.Opacity = IsSelected ? 1 : _isPointerOver ? 0.85 : 0.7;
        }

        bool isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (_selectionHighlighter is not null)
            _selectionHighlighter.Color = GetSelectionColor();

        if (_separator is not null)
        {
            _separator.Color = isDarkTheme
                ? Color.FromRgba(255, 255, 255, 34)
                : Color.FromRgba(0, 0, 0, 34);
            _separator.IsVisible = !IsSelected;
        }

        if (_closeButton is not null)
            _closeButton.IsVisible = IsClosable && (IsSelected || _isPointerOver);

        if (_rootGrid is not null)
        {
            _rootGrid.BackgroundColor = IsSelected
                ? (isDarkTheme ? Color.FromRgba(255, 255, 255, 34) : Color.FromRgba(0, 0, 0, 34))
                : _isPointerOver
                    ? (isDarkTheme ? Color.FromRgba(255, 255, 255, 21) : Color.FromRgba(0, 0, 0, 21))
                    : Colors.Transparent;
        }
    }

    private Color GetSelectionColor()
    {
        return IsSelected ? Colors.DodgerBlue : Colors.Transparent;
    }
}
