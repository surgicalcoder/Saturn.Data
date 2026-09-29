using System.Runtime.CompilerServices;

namespace Saturn.Data.DocumentDb;

internal static class AsyncEnumerableFactory
{
    public static async IAsyncEnumerable<T> From<T>(IEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
