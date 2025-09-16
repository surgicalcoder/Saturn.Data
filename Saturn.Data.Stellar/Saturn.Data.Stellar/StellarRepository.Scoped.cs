using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IScopedRepository
{
    public async Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var validIds = new List<EntityId>();
        
        foreach (var id in IDs)
        {
            var entity = collection.AsQueryable().FirstOrDefault(e => e.Id == id && e.Scope == scope);
            if (entity != null)
            {
                validIds.Add(new EntityId(id));
            }
        }
        
        await collection.RemoveBulkAsync(validIds);
    }

    public async Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entities.Select(e => { e.Scope = scope; return e; })
            .ToDictionary(entity => string.IsNullOrEmpty(entity.Id) ? new EntityId() : new EntityId(entity.Id), entity => entity);
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task Save<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await Upsert<TItem, TScope>(scope, entities, transaction, cancellationToken);
    }

    public async Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }

    public async Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var itemsToUpdate = collection.AsQueryable().Where(e => e.Scope == scope).Where(conditionPredicate).ToList();
        
        var updateTasks = itemsToUpdate.Select(async item =>
        {
            var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(System.Text.Json.JsonSerializer.Serialize(entity));
            updatedEntity.Scope = scope;
            updatedEntity.Id = item.Id;
            await collection.UpdateAsync(item.Id, updatedEntity);
        });
        
        await Task.WhenAll(updateTasks);
    }
    
    public async Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        
        var updateTasks = entities.Select(async entity =>
        {
            entity.Scope = scope;
            await collection.UpdateAsync(entity.Id, entity);
        });
        
        await Task.WhenAll(updateTasks);
    }

    public async Task Update<TItem, TScope>(string scope, List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        foreach (var entity in entities)
        {
            entity.Scope = scope;
            await collection.UpdateAsync(entity.Id, entity);
        }
    }

    public async Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityId = new EntityId(id);
        var entity = await ById<TItem, TScope>(scope, id);
    
        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with id '{id}' not found.");
        }
    
        if (entity.Scope != scope)
        {
            throw new InvalidOperationException("Scope mismatch.");
        }
    
        if (entity.Version != version)
        {
            throw new InvalidOperationException($"Version mismatch: expected {entity.Version}, got {version}.");
        }
    
        var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(json);
        if (updatedEntity == null)
        {
            throw new InvalidOperationException("Deserialization failed.");
        }
    
        updatedEntity.Id = id;
        updatedEntity.Scope = scope;
    
        await collection.UpdateAsync(id, updatedEntity);
    }

    public async Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await Upsert<TItem, TScope>(scope, entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (collection.ContainsKey(entity.Id))
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
        else
        {
            await collection.AddAsync(entity.Id, entity);
        }
    }

    public async Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var item in entity)
        {
            await Upsert<TItem, TScope>(scope, item, token: cancellationToken); // TODO make better with bulk
        }
    }
    

    public async Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem, TScope>(scope, id);
        if (entity != null && entity.Scope == scope)
        {
            await collection.RemoveAsync(id);
        }
    }

    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(e => e.Scope == scope).Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    public async Task DeleteMany<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var validEntities = entities.Where(e => e.Scope == scope);
        await collection.RemoveBulkAsync(validEntities.Select(e => new EntityId(e.Id)));
    }

    public async Task DeleteMany<TItem, TScope>(string scope, List<string> ids, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var validIds = new List<EntityId>();
        
        foreach (var id in ids)
        {
            var entity = collection.AsQueryable().FirstOrDefault(e => e.Id == id && e.Scope == scope);
            if (entity != null)
            {
                validIds.Add(new EntityId(id));
            }
        }
        
        await collection.RemoveBulkAsync(validIds);
    }
}