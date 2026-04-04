using System;
using Microsoft.Maui.Controls;

namespace MAUICustomControls.MacCatalyst.Controls;

public sealed class TabItem : TemplatedView
{
    private const string NormalStateName = "Normal";
    private const string PointerOverStateName = "PointerOver";
    private const string SelectedStateName = "Selected";

    private Grid? _rootGrid;
    private Button? _closeButton;
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
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_closeButton is not null) _closeButton.Clicked -= CloseButton_Clicked;

        _rootGrid = GetTemplateChild("RootGrid") as Grid;
        _closeButton = GetTemplateChild("CloseButton") as Button;

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
        if (_rootGrid is null)
            return;

        VisualStateManager.GoToState(_rootGrid, GetVisualStateName());
    }

    private string GetVisualStateName()
    {
        if (IsSelected)
            return SelectedStateName;

        return _isPointerOver ? PointerOverStateName : NormalStateName;
    }
}
