namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeTracked
{
    string Id { get; }

    long? Version { get; }
}

public interface ITrackable : IChangeTracked
{
    object? ChangeTrackingParent { get; set; }

    string? ChangeTrackingPathSegment { get; set; }

    bool IsTracking { get; }

    bool HasChanges { get; }

    void BeginTracking(bool acceptCurrentState = true);

    void AcceptChanges();

    void RejectChanges();

    EntityChangeSet GetChangeSet();

    string ToUpdateDocument();

    void CaptureBaseline();

    void RestoreBaseline();

    ChangeTracker GetTracker();

    IDisposable SuppressTracking();
}

public interface ITrackableMetadata
{
    ChangeVisibility VisibilityFor(string memberName);

    CollectionStrategy StrategyFor(string memberName);
}
