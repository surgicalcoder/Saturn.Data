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
    public void TrackedSet_Raises_Add_And_Remove()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add);

        set.Add("a");
        set.Remove("a");

        Assert.Contains(changes, change => change.Kind == ChangeKind.ListAdd && change.NewValue == "a");
        Assert.Contains(changes, change => change.Kind == ChangeKind.ListRemove && change.OldValue == "a");
    }

    [Fact]
    public void TrackedSet_Ignores_Duplicate_Add()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add) { "a" };

        changes.Clear();
        var added = set.Add("a");

        Assert.False(added);
        Assert.Empty(changes);
    }

    [Fact]
    public void TrackedDictionary_Raises_Set_And_Unset_With_Key()
    {
        var changes = new List<TrackedDictionaryChange<string, int>>();
        var dictionary = new TrackedDictionary<string, int>(changes.Add);

        dictionary["a"] = 1;
        dictionary["a"] = 2;
        dictionary.Remove("a");

        Assert.Equal(3, changes.Count);
        Assert.Equal(ChangeKind.Set, changes[0].Kind);
        Assert.Equal("a", changes[0].Key);
        Assert.Equal(2, changes[1].NewValue);
        Assert.Equal(ChangeKind.Unset, changes[2].Kind);
    }

    [Fact]
    public void TrackedSet_UnionWith_Raises_Adds_Only_For_New_Items()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add) { "a" };

        changes.Clear();
        set.UnionWith(new[] { "a", "b", "c" });

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(ChangeKind.ListAdd, change.Kind));
    }

    [Fact]
    public void TrackedSet_ExceptWith_Raises_Removes()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add) { "a", "b", "c" };

        changes.Clear();
        set.ExceptWith(new[] { "a", "z" });

        var change = Assert.Single(changes);
        Assert.Equal(ChangeKind.ListRemove, change.Kind);
        Assert.Equal("a", change.OldValue);
    }

    [Fact]
    public void TrackedSet_IntersectWith_Raises_Removes()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add) { "a", "b", "c" };

        changes.Clear();
        set.IntersectWith(new[] { "b" });

        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(ChangeKind.ListRemove, change.Kind));
    }

    [Fact]
    public void TrackedSet_SymmetricExceptWith_Toggles_Membership()
    {
        var changes = new List<TrackedCollectionChange<string>>();
        var set = new TrackedSet<string>(changes.Add) { "a", "b" };

        changes.Clear();
        set.SymmetricExceptWith(new[] { "b", "c" });

        Assert.Contains(changes, change => change.Kind == ChangeKind.ListRemove && Equals(change.OldValue, "b"));
        Assert.Contains(changes, change => change.Kind == ChangeKind.ListAdd && Equals(change.NewValue, "c"));
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
