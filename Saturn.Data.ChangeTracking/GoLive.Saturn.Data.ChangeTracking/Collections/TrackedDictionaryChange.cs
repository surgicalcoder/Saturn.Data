namespace GoLive.Saturn.Data.ChangeTracking;

public readonly struct TrackedDictionaryChange<TKey, TValue>
{
    public TrackedDictionaryChange(ChangeKind kind, TKey key, TValue oldValue, TValue newValue)
    {
        Kind = kind;
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public ChangeKind Kind { get; }

    public TKey Key { get; }

    public TValue OldValue { get; }

    public TValue NewValue { get; }
}
