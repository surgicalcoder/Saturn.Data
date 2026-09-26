namespace GoLive.Saturn.Data.ChangeTracking;

public interface IChangeTrackingObserver
{
    void OnChangeSetCaptured(string entityType, int fieldCount, int writeOnlyExcluded);

    void OnPatchConflict(string entityType, string id, long? expectedVersion);
}
