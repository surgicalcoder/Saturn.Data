namespace Saturn.Generator.Entities.Tests;

public class TrackingGenerationTests
{
    private const string TrackedSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }

            public partial int Count { get; set; }
        }
        """;

    private const string UntrackedSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }
        }
        """;

    private const string OptOutSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [NoChangeTracking]
        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }
        }
        """;

    [Fact]
    public void Tracking_Members_Emitted_When_Opted_In()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Tracked.g.cs");

        Assert.Contains("global::GoLive.Saturn.Data.ChangeTracking.ITrackable", generated);
        Assert.Contains("public void BeginTracking(bool acceptCurrentState = true)", generated);
        Assert.Contains("protected override void OnFieldChanged(string propertyName, object oldValue, object newValue)", generated);
        Assert.Contains("public void CaptureBaseline()", generated);
        Assert.Contains("public void RestoreBaseline()", generated);
        Assert.Contains("public global::GoLive.Saturn.Data.ChangeTracking.EntityChangeSet GetChangeSet()", generated);
    }

    [Fact]
    public void Tracking_Not_Emitted_By_Default()
    {
        var generated = GeneratorTestHarness.GeneratedFor(UntrackedSource, "Tracked.g.cs");

        Assert.DoesNotContain("ITrackable", generated);
        Assert.DoesNotContain("BeginTracking", generated);
    }

    [Fact]
    public void Assembly_Default_Enables_Tracking()
    {
        var generated = GeneratorTestHarness.GeneratedFor(UntrackedSource, "Tracked.g.cs", trackChangesByDefault: true);

        Assert.Contains("ITrackable", generated);
        Assert.Contains("BeginTracking", generated);
    }

    [Fact]
    public void NoChangeTracking_Opts_Out_Of_Assembly_Default()
    {
        var generated = GeneratorTestHarness.GeneratedFor(OptOutSource, "Tracked.g.cs", trackChangesByDefault: true);

        Assert.DoesNotContain("ITrackable", generated);
    }

    [Fact]
    public void Tracked_Entity_Emits_Baseline_Capture_For_Members()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Tracked.g.cs");

        Assert.Contains("tracker.CaptureValue(\"Name\", Name);", generated);
        Assert.Contains("tracker.CaptureValue(\"Count\", Count);", generated);
    }
}
