using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IScopedRepository : IScopedReadonlyRepository
{
    Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
        
    Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Save<TItem, TScope>(string scope, IEnumerable< TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
        
    Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
    
    Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new();
}