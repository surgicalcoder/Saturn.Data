namespace GoLive.Saturn.Data.ChangeTracking;

public sealed class FieldChange
{
    public string Path { get; set; }

    public ChangeKind Kind { get; set; }

    public object? OldValue { get; set; }

    public object? NewValue { get; set; }

    public ChangeVisibility Visibility { get; set; } = ChangeVisibility.ReadWrite;

    public int? Index { get; set; }

    public CollectionStrategy Strategy { get; set; } = CollectionStrategy.WholeArray;
}
