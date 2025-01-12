using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using MongoDB.Driver;

namespace GoLive.Saturn.Data.AsyncEnumerable;
public static class AsyncCursorExtensions
{
    /// <summary>
    ///     Wraps a cursor in an <see cref="IAsyncEnumerable{T}" /> that can be enumerated one time.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <param name="cursor">The cursor.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}" /></returns>
    public static IAsyncEnumerable<TDocument> ToAsyncEnumerable<TDocument>(this IAsyncCursor<TDocument> cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        return new AsyncCursorAsyncEnumerableOneTimeAdapter<TDocument>(cursor);
    }

    internal static async IAsyncEnumerable<TDocument> ToAsyncEnumerable<TDocument>(
        IAsyncCursorSource<TDocument>? source,
        IAsyncCursor<TDocument>? cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Debug.Assert(source is null ^ cursor is null, "Either source or cursor must be non-null, but not both");

        using (cursor ??= await source!.ToCursorAsync(cancellationToken).ConfigureAwait(false))
        {
            while (!cancellationToken.IsCancellationRequested && 
                   await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var document in cursor.Current)
                {
                    yield return document;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                }
            }
        }
    }

    private sealed class AsyncCursorAsyncEnumerableOneTimeAdapter<TDocument> : IAsyncEnumerable<TDocument>
    {
        private readonly IAsyncCursor<TDocument> _cursor;
        private int _enumerationCount;

        public AsyncCursorAsyncEnumerableOneTimeAdapter(IAsyncCursor<TDocument> cursor)
        {
            _cursor = cursor;
        }

        public IAsyncEnumerator<TDocument> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _enumerationCount) > 1)
            {
                throw new InvalidOperationException("An IAsyncCursor can only be enumerated once.");
            }

            return ToAsyncEnumerable(null, _cursor, cancellationToken).GetAsyncEnumerator(cancellationToken); 
        }
    }
}