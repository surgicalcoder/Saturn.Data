using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IScopedRepository : IScopedReadonlyRepository
{
    Task Insert<TItem, TScope>(string scope, TItem entity, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task InsertMany<TItem, TScope>(string scope, IEnumerable<TItem> entities, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    
    Task Update<TItem, TScope>(string scope, TItem entity, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task UpdateMany<TItem, TScope>(string scope, List<TItem> entity, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
        
    Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new();
        
    Task Upsert<TItem, TScope>(string scope, TItem entity, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task UpsertMany<TItem, TScope>(string scope, List<TItem> entity, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    
    Task Delete<TItem, TScope>(string scope, string id, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
    Task DeleteMany<TItem, TScope>(string scope, List<string> ds, CancellationToken cancellationToken = default)  where TItem : ScopedEntity<TScope> where TScope : Entity, new();
}