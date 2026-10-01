using GoLive.Saturn.Data.ChangeTracking;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class ChangeTrackingModeTests
{
    [Fact]
    public void Journal_Mode_Reports_Only_Journal()
    {
        var trackable = new ModeTrackable { TrackerMode = ChangeTrackingMode.Journal, Name = "start" };
        trackable.BeginTracking();
        trackable.Name = "changed";
        trackable.Record("Name", "start", "changed");

        var changeSet = trackable.GetChangeSet();

        var change = Assert.Single(changeSet.Fields);
        Assert.Equal(ChangeKind.Set, change.Kind);
    }

    [Fact]
    public void Baseline_Mode_Reports_Only_BaselineDiff()
    {
        var trackable = new ModeTrackable { TrackerMode = ChangeTrackingMode.Baseline, Name = "start" };
        trackable.BeginTracking();
        trackable.Name = "changed";

        var changeSet = trackable.GetChangeSet();

        var change = Assert.Single(changeSet.Fields);
        Assert.Equal("Name", change.Path);
        Assert.Equal("start", change.OldValue);
        Assert.Equal("changed", change.NewValue);
    }

    [Fact]
    public void JournalWithBaseline_Merges_Without_Duplicating_Paths()
    {
        var trackable = new ModeTrackable { TrackerMode = ChangeTrackingMode.JournalWithBaseline, Name = "start" };
        trackable.BeginTracking();
        trackable.Name = "changed";
        trackable.Record("Name", "start", "changed");

        var changeSet = trackable.GetChangeSet();

        Assert.Single(changeSet.Fields);
    }

    private sealed class ModeTrackable : ITrackable
    {
        private readonly ChangeTracker tracker = new();

        public string Id { get; set; } = "id";

        public long? Version { get; set; }

        public string? Name { get; set; }

        public object? ChangeTrackingParent { get; set; }

        public string? ChangeTrackingPathSegment { get; set; }

        public ChangeTrackingMode TrackerMode
        {
            get => tracker.Mode;
            set => tracker.Mode = value;
        }

        public bool IsTracking => tracker.IsTracking;

        public bool HasChanges => tracker.HasChanges;

        public void BeginTracking(bool acceptCurrentState = true) => tracker.Begin(this, acceptCurrentState);

        public void AcceptChanges() => tracker.Accept(this);

        public void RejectChanges() => tracker.Reject(this);

        public EntityChangeSet GetChangeSet() => tracker.Build(this);

        public string ToUpdateDocument() => tracker.Build(this).ToUpdateDocument();

        public void CaptureBaseline() => tracker.CaptureValue("Name", Name);

        public void RestoreBaseline() => Name = tracker.BaselineValue("Name") as string;

        public IEnumerable<FieldChange> ComputeBaselineDiff()
        {
            if (!Equals(tracker.BaselineValue("Name"), Name))
            {
                yield return new FieldChange
                {
                    Path = "Name",
                    Kind = ChangeKind.Set,
                    OldValue = tracker.BaselineValue("Name"),
                    NewValue = Name
                };
            }
        }

        public ChangeTracker GetTracker() => tracker;

        public IDisposable SuppressTracking() => tracker.Suppress();

        public void Record(string propertyName, object? oldValue, object? newValue) => tracker.Record(this, propertyName, oldValue, newValue);
    }
}
