using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ISecondScopedReadonlyRepository
{
    Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => All<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, transaction, cancellationToken);
    
    Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, id, transaction, cancellationToken);
    
    Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, IDs, transaction, cancellationToken);

    Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => Count<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, transaction, cancellationToken);

    async Task<bool> Exists<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => await ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) != null;

    async Task<bool> Exists<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => await Count<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, includeDeleted, transaction, cancellationToken).ConfigureAwait(false) > 0;
    
    IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, bool includeDeleted)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => IQueryable<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope);

    Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom = null, 
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();    

    Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    
    Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause,
        string continueFrom = null, 
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    
    Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => One<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, sortOrders, transaction, cancellationToken);
    
    Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, int count, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => Random<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, count, transaction, cancellationToken);

    async Task<IAsyncEnumerable<TProjection>> All<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await All<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> ById<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, id, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }

    async Task<IAsyncEnumerable<TProjection>> ById<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, ids, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber,
            sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<IAsyncEnumerable<TProjection>> Many<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var source = await Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber,
            sortOrders, includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return source.SelectAsync(selector.Compile(), cancellationToken);
    }

    async Task<TProjection> One<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);
        var item = await One<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, sortOrders,
            includeDeleted, transaction, cancellationToken).ConfigureAwait(false);
        return item == null ? default : selector.Compile().Invoke(item);
    }
    
}