namespace GoLive.Saturn.Data.Abstractions;

public enum RepositoryWriteOperation
{
    Insert,
    Update,
    Upsert,
    Save,
    Delete,
    HardDelete,
    Restore,
    Patch,
    Increment
}

