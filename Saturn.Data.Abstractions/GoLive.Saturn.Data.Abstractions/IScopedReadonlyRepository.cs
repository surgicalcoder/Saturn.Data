using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IScopedReadonlyRepository : IDisposable
{
    Task<TItem> ById<TItem, TScope>(TScope scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(TScope scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    
    Task<TItem> ById<TItem, TScope>(string scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new();

    Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new();

    IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new();

    Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new();

    Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
}