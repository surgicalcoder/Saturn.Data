using GoLive.Saturn.Data.ChangeTracking;

namespace GoLive.Saturn.Data.ChangeTracking.Tests;

public class CollectionInstrumentationTests
{
    [Fact]
    public void TrackedList_Raises_Indexed_Add()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var list = new TrackedList<string>(changes.Add);

        list.Add("a");

        var change = Assert.Single(changes);
        Assert.Equal(ChangeKind.ListAdd, change.Kind);
        Assert.Equal(0, change.Index);
        Assert.Equal("a", change.NewValue);
    }

    [Fact]
    public void TrackedList_Raises_Replace_And_Remove()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var list = new TrackedList<string>(changes.Add) { "a", "b" };

        list[1] = "c";
        list.RemoveAt(0);

        Assert.Contains(changes, change => change.Kind == ChangeKind.ListReplace && change.OldValue == "b" && change.NewValue == "c");
        Assert.Contains(changes, change => change.Kind == ChangeKind.ListRemove && change.OldValue == "a");
    }

    [Fact]
    public void TrackedList_Raises_Clear()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var list = new TrackedList<string>(changes.Add) { "a" };

        list.Clear();

        Assert.Contains(changes, change => change.Kind == ChangeKind.ListClear);
    }

    [Fact]
    public void CollectionDiff_ListEqual_Is_Positional()
    {
        Assert.True(CollectionDiff.ListEqual(new List<string> { "a", "b" }, new List<string> { "a", "b" }, StringComparer.Ordinal));
        Assert.False(CollectionDiff.ListEqual(new List<string> { "a", "b" }, new List<string> { "b", "a" }, StringComparer.Ordinal));
        Assert.False(CollectionDiff.ListEqual(new List<string> { "a" }, new List<string> { "a", "b" }, StringComparer.Ordinal));
    }

    [Fact]
    public void CollectionDiff_SetEqual_Is_Order_Insensitive()
    {
        Assert.True(CollectionDiff.SetEqual(new HashSet<string> { "a", "b" }, new[] { "b", "a" }));
        Assert.False(CollectionDiff.SetEqual(new HashSet<string> { "a" }, new[] { "a", "b" }));
    }

    [Fact]
    public void CollectionDiff_DictionaryEqual_Compares_Keys_And_Values()
    {
        var left = new Dictionary<string, int> { ["a"] = 1 };
        var right = new Dictionary<string, int> { ["a"] = 1 };
        var different = new Dictionary<string, int> { ["a"] = 2 };

        Assert.True(CollectionDiff.DictionaryEqual(left, right));
        Assert.False(CollectionDiff.DictionaryEqual(left, different));
    }

    [Fact]
    public void CollectionDiff_Null_Semantics()
    {
        Assert.True(CollectionDiff.ListEqual<string>(null, null, StringComparer.Ordinal));
        Assert.False(CollectionDiff.ListEqual<string>(null, new List<string>(), StringComparer.Ordinal));
    }
}
