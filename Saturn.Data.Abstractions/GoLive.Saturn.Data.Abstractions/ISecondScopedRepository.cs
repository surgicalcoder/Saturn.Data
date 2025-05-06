using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ISecondScopedRepository 
{
    Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
  

    Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    
    IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    

    Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    IQueryable<TItem> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, 
        int pageSize = 20, int pageNumber = 1, IEnumerable<SortOrder<TItem>> sortOrders = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();

    Task<long> CountMany<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    
    Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    
    Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    
    Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
    
    Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string Id, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new();
}