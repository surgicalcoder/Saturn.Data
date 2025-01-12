using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ITransparentScopedReadonlyRepository : IDisposable
{
    Task<TItem> ById<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<List<TItem>> ById<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();

    Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<IAsyncEnumerable<TItem>> All<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<TItem> Random<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null ) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();

    Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
}