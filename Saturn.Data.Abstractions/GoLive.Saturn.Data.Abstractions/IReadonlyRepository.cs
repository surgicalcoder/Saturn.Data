using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IReadonlyRepository : IDisposable
{
    Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => All<TItem>(transaction, cancellationToken);

    Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(id, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(IDs, transaction, cancellationToken);

    Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Count(predicate, continueFrom, transaction, cancellationToken);

    async Task<bool> Exists<TItem>(string id, bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => await ById<TItem>(id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) != null;

    async Task<bool> Exists<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => await Count(predicate, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) > 0;
    
    IQueryable<TItem> IQueryable<TItem>() where TItem : Entity;

    IQueryable<TItem> IQueryable<TItem>(bool includeDeleted)
        where TItem : Entity
        => IQueryable<TItem>();
    
    Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    
    Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => One(predicate, continueFrom, sortOrders, transaction, cancellationToken);

    Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Random(predicate, continueFrom, count, transaction, cancellationToken);

    async Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var compiledSelector = selector.Compile();
        var source = await All<TItem>(includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(compiledSelector, cancellationToken);
    }

    async Task<TProjection> ById<TItem, TProjection>(string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await ById<TItem>(id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);

        if (item == null)
        {
            return default;
        }

        return selector.Compile().Invoke(item);
    }

    async Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var compiledSelector = selector.Compile();
        var source = await ById<TItem>(ids, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(compiledSelector, cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var compiledSelector = selector.Compile();
        var source = await Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(compiledSelector, cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var compiledSelector = selector.Compile();
        var source = await Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(compiledSelector, cancellationToken);
    }

    async Task<TProjection> One<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await One(predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);

        if (item == null)
        {
            return default;
        }

        return selector.Compile().Invoke(item);
    }
}