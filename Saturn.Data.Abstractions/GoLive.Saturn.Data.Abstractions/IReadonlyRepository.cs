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
    Task<TItem> ById<TItem>(string id, CancellationToken cancellationToken = default) where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, CancellationToken cancellationToken = default) where TItem : Entity;

    Task<List<Ref<TItem>>> ByRef<TItem>(List<Ref<TItem>> item, CancellationToken cancellationToken = default) where TItem : Entity, new();
    Task<TItem> ByRef<TItem>(Ref<TItem> item, CancellationToken cancellationToken = default) where TItem : Entity, new();
    Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item, CancellationToken cancellationToken = default) where TItem : Entity, new();
        
    Task<IAsyncEnumerable<TItem>> All<TItem>(CancellationToken cancellationToken = default) where TItem : Entity;
    IQueryable<TItem> IQueryable<TItem>() where TItem : Entity;
    
    Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : Entity;
    Task<TItem> Random<TItem>(CancellationToken cancellationToken = default) where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> Random<TItem>(int count, CancellationToken cancellationToken = default) where TItem : Entity;
        
    IQueryable<TItem> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : Entity;
    IQueryable<TItem> Many<TItem>(Expression<Func<TItem, bool>> predicate,int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default) where TItem : Entity;
        
    Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken = default) where TItem : Entity;

    Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, CancellationToken cancellationToken = default) where TItem : Entity;
}