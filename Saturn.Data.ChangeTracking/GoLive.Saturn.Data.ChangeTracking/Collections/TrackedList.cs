using System.Collections;

namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class TrackedList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> inner;
    private readonly Action<TrackedCollectionChange<T>> onChanged;

    public TrackedList(Action<TrackedCollectionChange<T>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new List<T>();
    }

    public TrackedList(IEnumerable<T> collection, Action<TrackedCollectionChange<T>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new List<T>(collection);
    }

    public T this[int index]
    {
        get => inner[index];
        set
        {
            var previous = inner[index];
            inner[index] = value;
            onChanged(new TrackedCollectionChange<T>(ChangeKind.ListReplace, index, previous, value));
        }
    }

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        inner.Add(item);
        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListAdd, inner.Count - 1, default!, item));
    }

    public void AddRange(IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            Add(item);
        }
    }

    public void Insert(int index, T item)
    {
        inner.Insert(index, item);
        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListAdd, index, default!, item));
    }

    public bool Remove(T item)
    {
        var index = inner.IndexOf(item);

        if (!inner.Remove(item))
        {
            return false;
        }

        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListRemove, index, item, default!));
        return true;
    }

    public void RemoveAt(int index)
    {
        var previous = inner[index];
        inner.RemoveAt(index);
        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListRemove, index, previous, default!));
    }

    public void Clear()
    {
        if (inner.Count == 0)
        {
            return;
        }

        inner.Clear();
        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListClear, -1, default!, default!));
    }

    public bool Contains(T item) => inner.Contains(item);

    public int IndexOf(T item) => inner.IndexOf(item);

    public void CopyTo(T[] array, int arrayIndex) => inner.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();
}
