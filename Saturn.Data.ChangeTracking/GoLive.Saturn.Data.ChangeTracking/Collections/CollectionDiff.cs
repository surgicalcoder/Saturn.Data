using System.Collections;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class CollectionDiff
{
    public static bool ListEqual<T>(IList<T>? left, IList<T>? right, IEqualityComparer<T> comparer)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!comparer.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool SetEqual<T>(IEnumerable<T>? left, IEnumerable<T>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        var leftSet = new HashSet<T>(left);
        var rightSet = new HashSet<T>(right);

        return leftSet.SetEquals(rightSet);
    }

    public static bool DictionaryEqual<TKey, TValue>(IDictionary<TKey, TValue>? left, IDictionary<TKey, TValue>? right)
        where TKey : notnull
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !EqualityComparer<TValue>.Default.Equals(value, pair.Value))
            {
                return false;
            }
        }

        return true;
    }

    public static List<object?> Snapshot(IEnumerable source)
    {
        var result = new List<object?>();

        foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}
