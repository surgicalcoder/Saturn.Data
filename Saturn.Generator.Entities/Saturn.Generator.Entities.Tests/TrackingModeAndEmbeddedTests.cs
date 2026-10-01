namespace Saturn.Generator.Entities.Tests;

public class TrackingModeAndEmbeddedTests
{
    private const string BaselineModeSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking(Mode = ChangeTrackingMode.Baseline)]
        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }
        }
        """;

    private const string ScalarOnlySource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }
        }
        """;

    private const string EmbeddedSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        [GenerateDto]
        [ChangeTracking]
        public partial class Parent : Entity
        {
            [Embedded]
            public partial Ref<Child>? Child { get; set; }
        }

        [GenerateDto]
        public partial class Child : Entity
        {
            public partial string? Name { get; set; }
        }
        """;

    private const string ExistingDtoSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;
        using GoLive.Saturn.Generator.Entities.Resources;

        namespace Sample;

        [GenerateDto]
        public partial class Tracked : Entity
        {
            public partial string? Name { get; set; }
        }

        public sealed class TrackedDto
        {
        }
        """;

    [Fact]
    public void Explicit_Mode_Is_Emitted()
    {
        Assert.Contains("ChangeTrackingMode.Baseline", GeneratorTestHarness.GeneratedFor(BaselineModeSource, "Tracked.g.cs"));
    }

    [Fact]
    public void Scalar_Only_Entity_Defaults_To_Journal()
    {
        var generated = GeneratorTestHarness.GeneratedFor(ScalarOnlySource, "Tracked.g.cs");

        Assert.Contains("ChangeTrackingMode.Journal", generated);
        Assert.DoesNotContain("ChangeTrackingMode.JournalWithBaseline", generated);
    }

    [Fact]
    public void Scalar_Baseline_Diff_Is_Emitted()
    {
        var generated = GeneratorTestHarness.GeneratedFor(ScalarOnlySource, "Tracked.g.cs");

        Assert.Contains("Object.Equals(tracker.BaselineValue(\"Name\"), Name)", generated);
    }

    [Fact]
    public void Embedded_Ref_Expands_In_Dto()
    {
        Assert.Contains("ChildDto", GeneratorTestHarness.GeneratedFor(EmbeddedSource, "Parent.g.cs"));
    }

    [Fact]
    public void Embedded_Member_Wires_Child_Tracking()
    {
        Assert.Contains("ChangeTrackingPathSegment = nameof(Child)", GeneratorTestHarness.GeneratedFor(EmbeddedSource, "Parent.g.cs"));
    }

    [Fact]
    public void Existing_Dto_Suppresses_Generation()
    {
        Assert.DoesNotContain("DtoSchemaVersion", GeneratorTestHarness.GeneratedFor(ExistingDtoSource, "Tracked.g.cs"));
    }
}
