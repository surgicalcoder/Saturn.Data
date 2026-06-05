using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IWeakSecondScopedReadonlyRepository
{
    Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => All<TItem>(primaryScope, secondScope, transaction, cancellationToken);

    Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => ById<TItem>(primaryScope, secondScope, id, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => ById<TItem>(primaryScope, secondScope, IDs, transaction, cancellationToken);

    Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => Count<TItem>(primaryScope, secondScope, predicate, continueFrom, transaction, cancellationToken);

    async Task<bool> Exists<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => await ById<TItem>(primaryScope, secondScope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) != null;

    async Task<bool> Exists<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => await Count<TItem>(primaryScope, secondScope, predicate, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) > 0;

    IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new();

    IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted)
        where TItem : Entity, ISecondScopedById, new()
        => IQueryable<TItem>(primaryScope, secondScope);

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => Many<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => Many<TItem>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => One<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null,
        int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => Random<TItem>(primaryScope, secondScope, predicate, continueFrom, count, transaction, cancellationToken);

    async Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await All<TItem>(primaryScope, secondScope, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> ById<TItem, TProjection>(string primaryScope, string secondScope, string id,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await ById<TItem>(primaryScope, secondScope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }

    async Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(string primaryScope, string secondScope, IEnumerable<string> ids,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await ById<TItem>(primaryScope, secondScope, ids, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders,
            includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders,
            includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> One<TItem, TProjection>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await One<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }
}

