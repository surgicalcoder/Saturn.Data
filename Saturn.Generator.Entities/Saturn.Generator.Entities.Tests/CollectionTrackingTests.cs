namespace Saturn.Generator.Entities.Tests;

public class CollectionTrackingTests
{
    private const string TrackedSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Bag : Entity
        {
            private ObservableCollections.ObservableList<string> tags = new();
        }
        """;

    private const string StrategySource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Bag : Entity
        {
            [CollectionTracking(Strategy = CollectionStrategy.SetOps)]
            private ObservableCollections.ObservableList<string> tags = new();
        }
        """;

    private const string UntrackedSource = """
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        public partial class Bag : Entity
        {
            private ObservableCollections.ObservableList<string> tags = new();
        }
        """;

    [Fact]
    public void Tracked_Collection_Emits_Handler_And_Subscribes()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Bag.g.cs");

        Assert.Contains("private void OnTagsChanged(in global::ObservableCollections.NotifyCollectionChangedEventArgs<string> eventArgs)", generated);
        Assert.Contains("Tags.CollectionChanged += OnTagsChanged;", generated);
    }

    [Fact]
    public void Tracked_Collection_Setter_Resubscribes()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Bag.g.cs");

        Assert.Contains("tags.CollectionChanged -= OnTagsChanged;", generated);
        Assert.Contains("tags.CollectionChanged += OnTagsChanged;", generated);
    }

    [Fact]
    public void Tracked_Collection_Handler_Records_Structured_Ops()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Bag.g.cs");

        Assert.Contains("ChangeKind.ListAdd", generated);
        Assert.Contains("ChangeKind.ListRemove", generated);
        Assert.Contains("ChangeKind.ListReplace", generated);
        Assert.Contains("ChangeKind.ListMove", generated);
        Assert.Contains("ChangeKind.ListClear", generated);
    }

    [Fact]
    public void Collection_Strategy_Attribute_Is_Honoured()
    {
        var generated = GeneratorTestHarness.GeneratedFor(StrategySource, "Bag.g.cs");

        Assert.Contains("global::GoLive.Saturn.Data.ChangeTracking.CollectionStrategy.SetOps", generated);
    }

    private const string PlainListSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Bag : Entity
        {
            private List<string> scores = new();
        }
        """;

    [Fact]
    public void Plain_List_Emits_Baseline_Capture_And_Diff()
    {
        var generated = GeneratorTestHarness.GeneratedFor(PlainListSource, "Bag.g.cs");

        Assert.Contains("CaptureValue(\"Scores\"", generated);
        Assert.Contains("new List<string>(Scores)", generated);
        Assert.Contains("ComputeBaselineDiff()", generated);
        Assert.Contains("CollectionDiff.ListEqual", generated);
    }

    [Fact]
    public void Observable_Collection_Only_Emits_Empty_Baseline_Diff()
    {
        var generated = GeneratorTestHarness.GeneratedFor(TrackedSource, "Bag.g.cs");

        Assert.Contains("yield break;", generated);
        Assert.DoesNotContain("CollectionDiff.ListEqual", generated);
    }

    private const string InstrumentedSource = """
        using GoLive.Saturn.Data.ChangeTracking;
        using GoLive.Saturn.Data.Entities;

        namespace Sample;

        [ChangeTracking]
        public partial class Bag : Entity
        {
            [CollectionTracking(Instrument = true)]
            private List<string> list = new();

            [CollectionTracking(Instrument = true)]
            private HashSet<string> set = new();

            [CollectionTracking(Instrument = true)]
            private Dictionary<string, int> dictionary = new();
        }
        """;

    [Fact]
    public void Instrument_List_Emits_TrackedList_Property()
    {
        var generated = GeneratorTestHarness.GeneratedFor(InstrumentedSource, "Bag.g.cs");

        Assert.Contains("global::GoLive.Saturn.Data.ChangeTracking.TrackedList<string> List", generated);
        Assert.Contains("OnListChanged", generated);
    }

    [Fact]
    public void Instrument_Set_Emits_TrackedSet_Property()
    {
        var generated = GeneratorTestHarness.GeneratedFor(InstrumentedSource, "Bag.g.cs");

        Assert.Contains("global::GoLive.Saturn.Data.ChangeTracking.TrackedSet<string> Set", generated);
        Assert.Contains("OnSetChanged", generated);
    }

    [Fact]
    public void Instrument_Dictionary_Emits_TrackedDictionary_Property()
    {
        var generated = GeneratorTestHarness.GeneratedFor(InstrumentedSource, "Bag.g.cs");

        Assert.Contains("global::GoLive.Saturn.Data.ChangeTracking.TrackedDictionary<string, int> Dictionary", generated);
        Assert.Contains("OnDictionaryKeyChanged", generated);
    }

    [Fact]
    public void Instrumented_Collections_Are_Excluded_From_Baseline_Diff()
    {
        var generated = GeneratorTestHarness.GeneratedFor(InstrumentedSource, "Bag.g.cs");
        var index = generated.IndexOf("ComputeBaselineDiff", StringComparison.Ordinal);
        var block = generated[index..generated.IndexOf("VisibilityFor", index, StringComparison.Ordinal)];

        Assert.DoesNotContain("CollectionDiff.ListEqual", block);
    }

    [Fact]
    public void Untracked_Collection_Keeps_Legacy_Changes_Behaviour()
    {
        var generated = GeneratorTestHarness.GeneratedFor(UntrackedSource, "Bag.g.cs");

        Assert.DoesNotContain("OnTagsChanged", generated);
        Assert.Contains("Changes[$\"Tags.", generated);
    }
}
