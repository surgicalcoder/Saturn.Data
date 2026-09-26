namespace GoLive.Saturn.Data.ChangeTracking;

public readonly struct TrackedCollectionChange<T>
{
    public TrackedCollectionChange(ChangeKind kind, int index, T oldValue, T newValue)
    {
        Kind = kind;
        Index = index;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public ChangeKind Kind { get; }

    public int Index { get; }

    public T OldValue { get; }

    public T NewValue { get; }
}
