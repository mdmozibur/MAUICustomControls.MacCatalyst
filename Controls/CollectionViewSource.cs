using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MAUICustomControls.MacCatalyst.Controls;

/// <summary>
/// A lightweight replacement for UWP CollectionViewSource.
/// Wraps a grouped source collection and exposes it through the View property.
/// In MAUI, grouping is handled by CollectionView.IsGrouped, so this class
/// primarily serves as a bridge for converted UWP XAML.
/// </summary>
public sealed class CollectionViewSource : BindableObject
{
    private CollectionViewData? _view;

    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(IEnumerable), typeof(CollectionViewSource), null,
            propertyChanged: OnSourceChanged);

    public static readonly BindableProperty IsSourceGroupedProperty =
        BindableProperty.Create(nameof(IsSourceGrouped), typeof(bool), typeof(CollectionViewSource), false);

    public IEnumerable? Source
    {
        get => (IEnumerable?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsSourceGrouped
    {
        get => (bool)GetValue(IsSourceGroupedProperty);
        set => SetValue(IsSourceGroupedProperty, value);
    }

    /// <summary>
    /// Returns a view over the source that can be used as ItemsSource
    /// and exposes CollectionGroups for zoomed-out views.
    /// </summary>
    public CollectionViewData View
    {
        get
        {
            _view ??= new CollectionViewData(Source);
            return _view;
        }
    }

    /// <summary>
    /// Shortcut to <see cref="CollectionViewData.CollectionGroups"/>.
    /// </summary>
    public IReadOnlyList<CollectionViewGroup> CollectionGroups => View.CollectionGroups;

    private static void OnSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is CollectionViewSource cvs)
        {
            cvs._view = new CollectionViewData(newValue as IEnumerable);
            cvs.OnPropertyChanged(nameof(View));
            cvs.OnPropertyChanged(nameof(CollectionGroups));
        }
    }
}

/// <summary>
/// Wraps a grouped source collection, providing both IEnumerable iteration
/// (for use as ItemsSource) and a CollectionGroups property (for zoomed-out views).
/// </summary>
public sealed class CollectionViewData : IEnumerable, INotifyCollectionChanged
{
    private readonly IEnumerable? _source;

    public CollectionViewData(IEnumerable? source)
    {
        _source = source;

        if (_source is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (s, e) => CollectionChanged?.Invoke(this, e);
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Returns each top-level group wrapped in a <see cref="CollectionViewGroup"/>.
    /// </summary>
    public IReadOnlyList<CollectionViewGroup> CollectionGroups
    {
        get
        {
            if (_source is null)
                return Array.Empty<CollectionViewGroup>();

            var groups = new List<CollectionViewGroup>();
            foreach (var item in _source)
            {
                groups.Add(new CollectionViewGroup(item));
            }
            return groups;
        }
    }

    public IEnumerator GetEnumerator() => _source?.GetEnumerator() ?? Enumerable.Empty<object>().GetEnumerator();
}

/// <summary>
/// Wraps a single group from a grouped collection, matching UWP's ICollectionViewGroup interface.
/// The Group property returns the original group object.
/// </summary>
public sealed class CollectionViewGroup
{
    public CollectionViewGroup(object? group)
    {
        Group = group;
    }

    public object? Group { get; }
}
