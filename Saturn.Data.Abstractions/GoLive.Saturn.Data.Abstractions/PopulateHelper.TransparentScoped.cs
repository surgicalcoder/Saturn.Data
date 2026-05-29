using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static partial class PopulateHelper
{
    public static async Task Populate<TItem, TParent>(this Ref<TItem> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id) || repository == null)
            return;

        item.Item = await repository.ById<TItem, TParent>(item.Id, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static async Task Populate<TItem, TParent>(this IList<Ref<TItem>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
            return;

        var ids = item.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var items = await (await repository.ById<TItem, TParent>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < item.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            item[i].Fetch(items);
        }
    }

    public static async Task<IList<TItem>> Populate<TItem, T2, TParent>(this IList<TItem> collection, Expression<Func<TItem, Ref<T2>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
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

        var items = await (await repository.ById<T2, TParent>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < refs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            refs[i]?.Fetch(items);
        }

        return collection;
    }

    public static Task<IList<TItem>> PopulateMultiple<TItem, T2, TParent>(this IList<TItem> collection, Expression<Func<TItem, List<Ref<T2>>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => PopulateMultipleScopedCore<TItem, T2, TParent>(collection, GetOrCompile(item), repository, transaction, cancellationToken);

    public static Task<IList<TItem>> PopulateMultiple<TItem, T2, TParent>(this IList<TItem> collection, Expression<Func<TItem, IList<Ref<T2>>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => PopulateMultipleScopedCore<TItem, T2, TParent>(collection, GetOrCompile(item), repository, transaction, cancellationToken);

    private static async Task<IList<T>> PopulateMultipleScopedCore<T, T2, TParent>(IList<T> collection, Func<T, IEnumerable<Ref<T2>>> compiledFunc, ITransparentScopedRepository repository, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (collection == null || collection.Count == 0 || repository == null)
            return collection;

        var ids = collection
            .SelectMany(f => compiledFunc(f) ?? Enumerable.Empty<Ref<T2>>())
            .Select(r => r.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var items = await (await repository.ById<T2, TParent>(ids, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false)).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

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
}
