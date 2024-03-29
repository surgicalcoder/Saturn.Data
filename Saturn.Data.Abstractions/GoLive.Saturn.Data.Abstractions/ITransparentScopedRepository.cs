using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface ITransparentScopedRepository : IReadonlyRepository
{
    Task Insert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task InsertMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task Save<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task SaveMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task Update<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task UpdateMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();

    Task Upsert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task UpsertMany<TItem, TParent>(List<TItem> entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task Delete<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task Delete<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task DeleteMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
    Task DeleteMany<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
        
    Task JsonUpdate<TItem, TParent>(string id, int version, string json) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new();
}