namespace MAUICustomControls.MacCatalyst.Platforms.Linux;

using System.Collections;
using System.Collections.Specialized;

/// <summary>
/// A read-only list over a collection that raises CollectionChanged without being an IList (a
/// grouped or filtered view): Avalonia's items controls need to index the source. It takes a copy
/// of the items and, whenever the source changes, takes a new one and reports a reset.
/// </summary>
internal sealed class NotifyingListAdapter : IList, INotifyCollectionChanged
{
    private readonly IEnumerable _source;
    private List<object?> _items;

    public NotifyingListAdapter(IEnumerable source)
    {
        _source = source;
        _items = source.Cast<object?>().ToList();
        if (source is INotifyCollectionChanged notifying)
        {
            notifying.CollectionChanged += OnSourceChanged;
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => _items.Count;

    public bool IsReadOnly => true;

    public bool IsFixedSize => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public object? this[int index]
    {
        get => _items[index];
        set => throw new NotSupportedException();
    }

    public bool Contains(object? value) => _items.Contains(value);

    public int IndexOf(object? value) => _items.IndexOf(value);

    public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public IEnumerator GetEnumerator() => _items.GetEnumerator();

    public int Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public void Insert(int index, object? value) => throw new NotSupportedException();

    public void Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _items = _source.Cast<object?>().ToList();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
