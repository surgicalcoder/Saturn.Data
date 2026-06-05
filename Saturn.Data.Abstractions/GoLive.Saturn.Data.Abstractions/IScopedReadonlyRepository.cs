using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IScopedReadonlyRepository : IDisposable
{
    Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => All<TItem, TScope>(scope, transaction, cancellationToken);

    Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<TItem> ById<TItem, TScope>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ById<TItem, TScope>(scope, id, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ById<TItem, TScope>(scope, IDs, transaction, cancellationToken);

    Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Count<TItem, TScope>(scope, predicate, continueFrom, transaction, cancellationToken);

    async Task<bool> Exists<TItem, TScope>(string scope, string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => await ById<TItem, TScope>(scope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) != null;

    async Task<bool> Exists<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => await Count<TItem, TScope>(scope, predicate, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) > 0;

    IQueryable<TItem> IQueryable<TItem, TScope>(string scope)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TScope>(string scope, bool includeDeleted)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => IQueryable<TItem, TScope>(scope);

    Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Many<TItem, TScope>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    
    Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Many<TItem, TScope>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => One<TItem, TScope>(scope, predicate, continueFrom, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => Random<TItem, TScope>(scope, predicate, continueFrom, count, transaction, cancellationToken);

    async Task<IAsyncEnumerable<TProjection>> All<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await All<TItem, TScope>(scope, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> ById<TItem, TScope, TProjection>(string scope, string id, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await ById<TItem, TScope>(scope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }

    async Task<IAsyncEnumerable<TProjection>> ById<TItem, TScope, TProjection>(string scope, IEnumerable<string> ids,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await ById<TItem, TScope>(scope, ids, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TScope>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Dictionary<string, object> whereClause,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TScope>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> One<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await One<TItem, TScope>(scope, predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }
}