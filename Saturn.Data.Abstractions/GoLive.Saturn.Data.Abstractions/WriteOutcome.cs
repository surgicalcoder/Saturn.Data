namespace GoLive.Saturn.Data.Abstractions;

public enum WriteOutcome
{
    Inserted,
    Updated,
    Merged,
    Deleted,
    Restored,
    Patched,
    Incremented
}
