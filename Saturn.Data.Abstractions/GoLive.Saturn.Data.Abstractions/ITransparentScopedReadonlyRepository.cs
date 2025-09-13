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
    
    Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TParent>()
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();
}