using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ITransparentScopedRepository : IReadonlyRepository
{
    Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();

    Task Delete<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Insert<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
        
    Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
        
    Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Save<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
        
    Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Update<TItem, TParent>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Update<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();

    Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
    
    Task Upsert<TItem, TParent>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new();
}