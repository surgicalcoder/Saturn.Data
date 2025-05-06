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
    Task<TItem> ById<TItem, TParent>(string id, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<List<TItem>> ById<TItem, TParent>(List<string> IDs, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();

    Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<TItem> Random<TItem, TParent>(CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();

    Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
}