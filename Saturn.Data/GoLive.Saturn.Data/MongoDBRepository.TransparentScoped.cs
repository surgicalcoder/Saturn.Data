using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : ITransparentScopedRepository
{
    public async Task Insert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Insert<TItem, TParent>(scope, entity);
    }

    public async Task InsertMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await InsertMany<TItem, TParent>(scope, entities);
    }

    public async Task Save<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Upsert<TItem, TParent>(entity);
    }

    public async Task SaveMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpsertMany<TItem, TParent>(scope, entities);
    }

    public async Task Update<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, entity);
    }

    public async Task UpdateMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entities);
    }

    public async Task Upsert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Upsert<TItem, TParent>(scope, entity);
    }

    async Task ITransparentScopedRepository.UpsertMany<TItem, TParent>(List<TItem> entity)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entity);
    }

    /*public async Task UpsertMany<TItem, TParent>(List<TItem> entity) where TItem : ScopedEntity<TParent> where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entity);
    }*/

    public async Task Delete<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, entity);
    }

    public async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, filter);
    }

    public async Task Delete<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, id);
    }

    public async Task DeleteMany<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, IDs);
    }

    public async Task JsonUpdate<TItem, TParent>(string id, int version, string json) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await JsonUpdate<TItem, TParent>(scope, id, version, json);
    }
    
    
    public async Task DeleteMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, entities);
    }
}