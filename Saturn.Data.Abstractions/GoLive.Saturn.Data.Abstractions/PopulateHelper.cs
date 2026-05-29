using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FastExpressionCompiler;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static partial class PopulateHelper
{
    // Cache keyed by expression instance reference. Effective when callers use static readonly expressions.
    private static readonly ConcurrentDictionary<LambdaExpression, Delegate> CompiledFuncCache = new();

    private static Func<T, TResult> GetOrCompile<T, TResult>(Expression<Func<T, TResult>> expression)
        => (Func<T, TResult>)CompiledFuncCache.GetOrAdd(expression, e => ((Expression<Func<T, TResult>>)e).CompileFast());

    public static async Task Populate<T>(this Ref<T> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id) || repository == null)
            return;

        item.Item = await repository.ById<T>(item.Id, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static void Populate<T>(this Ref<T> item, IList<T> items) where T : Entity, new()
    {
        if (item == null || items == null || string.IsNullOrWhiteSpace(item.Id))
            return;

        item.Item = items.FirstOrDefault(f => f.Id == item.Id);
    }

    public static async Task Populate<T>(this IList<Ref<T>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
            return;

        var ids = item.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var items = await (await repository.ById<T>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < item.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            item[i].Fetch(items);
        }
    }

    public static void Populate<T>(this IList<Ref<T>> item, IList<T> items) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || items == null || items.Count == 0)
            return;

        for (var i = 0; i < item.Count; i++)
            item[i].Fetch(items);
    }

    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
            return collection;

        var compiledFunc = GetOrCompile(item);
        var refs = collection.Select(f => compiledFunc(f)).ToList();

        var ids = refs
            .Where(r => r != null)
            .Select(r => r.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return collection;

        var items = await (await repository.ById<T2>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < refs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            refs[i]?.Fetch(items);
        }

        return collection;
    }

    public static IList<T> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IList<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || items == null || items.Count == 0)
            return collection;

        var compiledFunc = GetOrCompile(item);

        for (var i = 0; i < collection.Count; i++)
            compiledFunc(collection[i])?.Fetch(items);

        return collection;
    }

    public static Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, List<Ref<T2>>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
        => PopulateMultipleCore<T, T2>(collection, GetOrCompile(item), repository, transaction, cancellationToken);

    public static Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
        => PopulateMultipleCore<T, T2>(collection, GetOrCompile(item), repository, transaction, cancellationToken);

    private static async Task<IList<T>> PopulateMultipleCore<T, T2>(IList<T> collection, Func<T, IEnumerable<Ref<T2>>> compiledFunc, IReadonlyRepository repository, IDatabaseTransaction transaction, CancellationToken cancellationToken) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || repository == null)
            return collection;

        var ids = collection
            .SelectMany(f => compiledFunc(f) ?? Enumerable.Empty<Ref<T2>>())
            .Select(r => r.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var items = await (await repository.ById<T2>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < collection.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = compiledFunc(collection[i]);
            if (result == null) continue;
            foreach (var r2 in result)
                r2.Fetch(items);
        }

        return collection;
    }

    public static IList<T> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, List<Ref<T2>>>> item, IList<T2> items) where T2 : Entity, new()
        => PopulateMultipleSyncCore<T, T2>(collection, GetOrCompile(item), items);

    public static IList<T> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IList<T2> items) where T2 : Entity, new()
        => PopulateMultipleSyncCore<T, T2>(collection, GetOrCompile(item), items);

    private static IList<T> PopulateMultipleSyncCore<T, T2>(IList<T> collection, Func<T, IEnumerable<Ref<T2>>> compiledFunc, IList<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || items == null || items.Count == 0)
            return collection;

        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compiledFunc(collection[i]) ?? Enumerable.Empty<Ref<T2>>())
                r2.Fetch(items);
        }

        return collection;
    }
}