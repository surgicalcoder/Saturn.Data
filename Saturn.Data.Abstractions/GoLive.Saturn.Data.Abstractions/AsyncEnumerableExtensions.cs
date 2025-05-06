using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions;

/// <summary>
/// Extension methods for IAsyncEnumerable.
/// </summary>
public static class AsyncEnumerableExtensions
{
    /// <summary>
    /// Converts an IAsyncEnumerable<T> to a List<T> asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The source IAsyncEnumerable<T>.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a List<T>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if source is null.</exception>
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        if (source == null) 
            throw new ArgumentNullException(nameof(source));
            
        var result = new List<T>();
            
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            result.Add(item);
        }
            
        return result;
    }
}