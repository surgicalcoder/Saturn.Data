using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GoLive.Saturn.Data.Abstractions;

public static class AsyncEnumerableProjectionExtensions
{
    public static async IAsyncEnumerable<TOutput> SelectAsync<TInput, TOutput>(this IAsyncEnumerable<TInput> source, Func<TInput, TOutput> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            yield return selector(enumerator.Current);
        }
    }
}


