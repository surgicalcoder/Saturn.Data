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
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TScope>(string scope)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>
        where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(string scope, int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new();
}