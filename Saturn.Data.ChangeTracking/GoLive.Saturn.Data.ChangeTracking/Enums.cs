namespace GoLive.Saturn.Data.ChangeTracking;

public enum ChangeKind
{
    Set,
    Unset,
    Increment,
    ListAdd,
    ListRemove,
    ListReplace,
    ListMove,
    ListClear
}

public enum ChangeVisibility
{
    ReadWrite,
    ReadOnly,
    WriteOnly,
    ServerManaged
}

public enum CollectionStrategy
{
    WholeArray,
    IndexedOps,
    SetOps
}

public enum ChangeTrackingMode
{
    Journal,
    Baseline,
    JournalWithBaseline
}
