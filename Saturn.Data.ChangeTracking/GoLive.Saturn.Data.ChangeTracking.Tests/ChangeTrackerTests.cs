using GoLive.Saturn.Data.ChangeTracking;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class ChangeTrackerTests
{
    [Fact]
    public void Scalar_Change_Records_Path_And_Values()
    {
        var trackable = new FakeTrackable();
        trackable.BeginTracking();
        trackable.Record("Name", "before", "after");

        var change = Assert.Single(trackable.GetTracker().Journal);
        Assert.Equal("Name", change.Path);
        Assert.Equal(ChangeKind.Set, change.Kind);
        Assert.Equal("before", change.OldValue);
        Assert.Equal("after", change.NewValue);
    }

    [Fact]
    public void Id_And_Version_Are_Not_Journaled()
    {
        var trackable = new FakeTrackable();
        trackable.BeginTracking();
        trackable.Record("Id", "a", "b");
        trackable.Record("Version", 1, 2);

        Assert.False(trackable.HasChanges);
    }

    [Fact]
    public void Ref_Change_Records_Id_String()
    {
        var trackable = new FakeTrackable();
        trackable.BeginTracking();
        var reference = new Ref<FakeEntity>("507f1f77bcf86cd799439011");

        trackable.Record("Parent", null, reference);

        var change = Assert.Single(trackable.GetTracker().Journal);
        Assert.Equal("507f1f77bcf86cd799439011", change.NewValue);
    }

    [Fact]
    public void Not_Tracking_Does_Not_Journal()
    {
        var trackable = new FakeTrackable();
        trackable.Record("Name", "before", "after");

        Assert.False(trackable.HasChanges);
    }

    [Fact]
    public void Hydration_Does_Not_Journal()
    {
        var trackable = new FakeTrackable();
        trackable.BeginTracking();

        using (trackable.SuppressTracking())
        {
            trackable.Record("Name", "before", "after");
        }

        Assert.False(trackable.HasChanges);
    }

    [Fact]
    public void AcceptChanges_Clears_Journal_And_Captures_Baseline()
    {
        var trackable = new FakeTrackable { Name = "current" };
        trackable.BeginTracking();
        trackable.Record("Name", "before", "after");

        trackable.AcceptChanges();

        Assert.False(trackable.HasChanges);
        Assert.Equal("current", trackable.GetTracker().BaselineValue("Name"));
    }

    [Fact]
    public void RejectChanges_Restores_Baseline()
    {
        var trackable = new FakeTrackable { Name = "original" };
        trackable.BeginTracking();
        trackable.Name = "changed";
        trackable.Record("Name", "original", "changed");

        trackable.RejectChanges();

        Assert.Equal("original", trackable.Name);
        Assert.False(trackable.HasChanges);
    }

    [Fact]
    public void Build_Populates_EntityChangeSet()
    {
        var trackable = new FakeTrackable { Id = "abc", Version = 3 };
        trackable.BeginTracking();
        trackable.Record("Name", null, "value");

        var changeSet = trackable.GetChangeSet();

        Assert.Equal("FakeTrackable", changeSet.EntityType);
        Assert.Equal("abc", changeSet.Id);
        Assert.Equal(3, changeSet.ExpectedVersion);
        Assert.Single(changeSet.Fields);
    }

    [Fact]
    public void Begin_Without_Baseline_Does_Not_Capture()
    {
        var trackable = new FakeTrackable { Name = "current" };
        trackable.BeginTracking(acceptCurrentState: false);

        Assert.Null(trackable.GetTracker().BaselineValue("Name"));
    }

    private sealed class FakeEntity : Entity
    {
    }

    private sealed class FakeTrackable : ITrackable
    {
        private readonly ChangeTracker tracker = new();

        public string Id { get; set; } = "id";

        public long? Version { get; set; }

        public string? Name { get; set; }

        public object? ChangeTrackingParent { get; set; }

        public string? ChangeTrackingPathSegment { get; set; }

        public bool IsTracking => tracker.IsTracking;

        public bool HasChanges => tracker.HasChanges;

        public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);

        public void AcceptChanges() => tracker.Accept(this);

        public void RejectChanges() => tracker.Reject(this);

        public EntityChangeSet GetChangeSet() => tracker.Build(this);

        public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();

        public void CaptureBaseline() => tracker.CaptureValue("Name", Name);

        public void RestoreBaseline() => Name = tracker.BaselineValue("Name") as string;

        public IEnumerable<FieldChange> ComputeBaselineDiff() => Array.Empty<FieldChange>();

        public ChangeTracker GetTracker() => tracker;

        public IDisposable SuppressTracking() => tracker.Suppress();

        public void Record(string propertyName, object? oldValue, object? newValue) => tracker.Record(this, propertyName, oldValue, newValue);
    }
}
