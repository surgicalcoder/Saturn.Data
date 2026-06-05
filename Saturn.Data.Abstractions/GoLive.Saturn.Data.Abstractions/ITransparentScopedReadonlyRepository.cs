using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ITransparentScopedReadonlyRepository : IDisposable
{
    Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => All<TItem, TParent>(transaction, cancellationToken);
    
    Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<TItem> ById<TItem, TParent>(string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ById<TItem, TParent>(id, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ById<TItem, TParent>(IDs, transaction, cancellationToken);

    Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => Count<TItem, TParent>(predicate, continueFrom, transaction, cancellationToken);

    async Task<bool> Exists<TItem, TParent>(string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => await ById<TItem, TParent>(id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) != null;

    async Task<bool> Exists<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => await Count<TItem, TParent>(predicate, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) > 0;

    IQueryable<TItem> IQueryable<TItem, TParent>()
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TParent>(bool includeDeleted)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => IQueryable<TItem, TParent>();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => Many<TItem, TParent>(predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => Many<TItem, TParent>(whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => One<TItem, TParent>(predicate, continueFrom, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => Random<TItem, TParent>(predicate, continueFrom, count, transaction, cancellationToken);

    async Task<IAsyncEnumerable<TProjection>> All<TItem, TParent, TProjection>(Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await All<TItem, TParent>(includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> ById<TItem, TParent, TProjection>(string id, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await ById<TItem, TParent>(id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }

    async Task<IAsyncEnumerable<TProjection>> ById<TItem, TParent, TProjection>(IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await ById<TItem, TParent>(ids, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TParent, TProjection>(Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TParent>(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TParent, TProjection>(Dictionary<string, object> whereClause,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TParent>(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> One<TItem, TParent, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await One<TItem, TParent>(predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }
}