using System.Collections;

namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class TrackedSet<T> : ISet<T>, IReadOnlyCollection<T>
{
    private readonly HashSet<T> inner;
    private readonly Action<TrackedCollectionChange<T>> onChanged;

    public TrackedSet(Action<TrackedCollectionChange<T>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new HashSet<T>();
    }

    public TrackedSet(IEnumerable<T> collection, Action<TrackedCollectionChange<T>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new HashSet<T>(collection);
    }

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public bool Add(T item)
    {
        if (!inner.Add(item))
        {
            return false;
        }

        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListAdd, -1, default!, item));
        return true;
    }

    void ICollection<T>.Add(T item) => Add(item);

    public bool Remove(T item)
    {
        if (!inner.Remove(item))
        {
            return false;
        }

        onChanged(new TrackedCollectionChange<T>(ChangeKind.ListRemove, -1, item, default!));
        return true;
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

    public void CopyTo(T[] array, int arrayIndex) => inner.CopyTo(array, arrayIndex);

    public void ExceptWith(IEnumerable<T> other) => inner.ExceptWith(other);

    public void IntersectWith(IEnumerable<T> other) => inner.IntersectWith(other);

    public bool IsProperSubsetOf(IEnumerable<T> other) => inner.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<T> other) => inner.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<T> other) => inner.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<T> other) => inner.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<T> other) => inner.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => inner.SetEquals(other);

    public void SymmetricExceptWith(IEnumerable<T> other) => inner.SymmetricExceptWith(other);

    public void UnionWith(IEnumerable<T> other) => inner.UnionWith(other);

    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();
}
