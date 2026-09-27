using System.Collections;

namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class TrackedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> inner;
    private readonly Action<TrackedDictionaryChange<TKey, TValue>> onChanged;

    public TrackedDictionary(Action<TrackedDictionaryChange<TKey, TValue>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new Dictionary<TKey, TValue>();
    }

    public TrackedDictionary(IDictionary<TKey, TValue> source, Action<TrackedDictionaryChange<TKey, TValue>> onChanged)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        inner = new Dictionary<TKey, TValue>(source);
    }

    public TValue this[TKey key]
    {
        get => inner[key];
        set
        {
            var hadValue = inner.TryGetValue(key, out var previous);
            inner[key] = value;
            onChanged(new TrackedDictionaryChange<TKey, TValue>(ChangeKind.Set, key, hadValue ? previous! : default!, value));
        }
    }

    public ICollection<TKey> Keys => inner.Keys;

    public ICollection<TValue> Values => inner.Values;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => inner.Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => inner.Values;

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        inner.Add(key, value);
        onChanged(new TrackedDictionaryChange<TKey, TValue>(ChangeKind.Set, key, default!, value));
    }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public bool Remove(TKey key)
    {
        if (!inner.TryGetValue(key, out var previous))
        {
            return false;
        }

        inner.Remove(key);
        onChanged(new TrackedDictionaryChange<TKey, TValue>(ChangeKind.Unset, key, previous, default!));
        return true;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public void Clear()
    {
        foreach (var key in inner.Keys.ToList())
        {
            Remove(key);
        }
    }

    public bool ContainsKey(TKey key) => inner.ContainsKey(key);

    public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)inner).Contains(item);

    public bool TryGetValue(TKey key, out TValue value) => inner.TryGetValue(key, out value!);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)inner).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();
}
