using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class ChangeTrackingObserverTests
{
    [Fact]
    public void Observer_Receives_ChangeSet()
    {
        var observer = new RecordingObserver();
        var trackable = new FakeTrackable { Observer = observer };
        trackable.BeginTracking();
        trackable.Record("Name", null, "value");

        var changeSet = trackable.GetChangeSet();

        Assert.Single(changeSet.Fields);
        var captured = Assert.Single(observer.Captured);
        Assert.Equal(nameof(FakeTrackable), captured.EntityType);
        Assert.Equal(1, captured.FieldCount);
    }

    [Fact]
    public async Task Observer_Receives_Patch_Conflict()
    {
        var observer = new RecordingObserver();
        var repository = DispatchProxy.Create<IRepository, ThrowingRepositoryProxy>();
        var changes = new EntityChangeSet { EntityType = nameof(FakeEntity), Id = "abc" };

        await Assert.ThrowsAsync<FailedToUpdateException>(() =>
            repository.PatchChanges<FakeEntity>("abc", 3, changes, observer));

        var conflict = Assert.Single(observer.Conflicts);
        Assert.Equal(nameof(FakeEntity), conflict.EntityType);
        Assert.Equal("abc", conflict.Id);
        Assert.Equal(3, conflict.ExpectedVersion);
    }

    [Fact]
    public void Observer_Is_Null_Safe()
    {
        var trackable = new FakeTrackable { Observer = null };
        trackable.BeginTracking();
        trackable.Record("Name", null, "value");

        Assert.NotNull(trackable.GetChangeSet());
    }

    private sealed class FakeEntity : Entity
    {
    }

    private sealed class RecordingObserver : IChangeTrackingObserver
    {
        public List<(string EntityType, int FieldCount, int WriteOnlyExcluded)> Captured { get; } = new();

        public List<(string EntityType, string Id, long? ExpectedVersion)> Conflicts { get; } = new();

        public void OnChangeSetCaptured(string entityType, int fieldCount, int writeOnlyExcluded) => Captured.Add((entityType, fieldCount, writeOnlyExcluded));

        public void OnPatchConflict(string entityType, string id, long? expectedVersion) => Conflicts.Add((entityType, id, expectedVersion));
    }

    public class ThrowingRepositoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new FailedToUpdateException();
    }

    private sealed class FakeTrackable : ITrackable
    {
        private readonly ChangeTracker tracker = new();

        public IChangeTrackingObserver? Observer
        {
            get => tracker.Observer;
            set => tracker.Observer = value;
        }

        public string Id { get; set; } = "id";

        public long? Version { get; set; }

        public object? ChangeTrackingParent { get; set; }

        public string? ChangeTrackingPathSegment { get; set; }

        public bool IsTracking => tracker.IsTracking;

        public bool HasChanges => tracker.HasChanges;

        public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);

        public void AcceptChanges() => tracker.Accept(this);

        public void RejectChanges() => tracker.Reject(this);

        public EntityChangeSet GetChangeSet() => tracker.Build(this);

        public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();

        public void CaptureBaseline()
        {
        }

        public void RestoreBaseline()
        {
        }

        public IEnumerable<FieldChange> ComputeBaselineDiff() => Array.Empty<FieldChange>();

        public ChangeTracker GetTracker() => tracker;

        public IDisposable SuppressTracking() => tracker.Suppress();

        public void Record(string propertyName, object? oldValue, object? newValue) => tracker.Record(this, propertyName, oldValue, newValue);
    }
}
