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

    [Fact]
    public void Untracked_Collection_Keeps_Legacy_Changes_Behaviour()
    {
        var generated = GeneratorTestHarness.GeneratedFor(UntrackedSource, "Bag.g.cs");

        Assert.DoesNotContain("OnTagsChanged", generated);
        Assert.Contains("Changes[$\"Tags.", generated);
    }
}
