using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IRepository
{
    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(IDs.Select(id => new EntityId(id)));
    }

    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entities.ToDictionary(
            entity => string.IsNullOrEmpty(entity.Id) ? new EntityId() : new EntityId(entity.Id),
            entity => entity
        );
        
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        await Upsert(entity, transaction, token);
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entities.ToList();
        await Save(entityList, token: cancellationToken);
    }

    public async Task Save<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
      var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
      
      var entitiesToUpdate = entities.Where(entity => !string.IsNullOrEmpty(entity.Id)).ToList();
      var entitiesToAdd = entities.Where(entity => string.IsNullOrEmpty(entity.Id)).ToList();

      await Update(entitiesToUpdate, token: token);
      await Insert(entitiesToAdd, token: token);

    }
    
    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        await Save(entity, token: token);
    }
    
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(conditionPredicate).ToList();
        foreach (var item in items)
        {
            await collection.UpdateAsync(item.Id, entity);
        }
    }

    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entities.ToList();
        await Update(entityList, token: cancellationToken);
    }

    public async Task Update<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        await Save(entities, token: token);
    }
    
    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
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

    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entity.ToList();
        await Upsert(entityList, token: cancellationToken);
    }

    public async Task Upsert<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        foreach (var entity in entities)
        {
            await Upsert(entity, token: token);
        }
    }
    
    public async Task Delete<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveAsync(entity.Id);
    }
    
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(r=>new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }
    
    public async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveAsync(id);
    }
    
    public async Task DeleteMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        
        await collection.RemoveBulkAsync(entities.Select(e => new EntityId(e.Id)));
    }
    
    public async Task DeleteMany<TItem>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(IDs.Select(id => new EntityId(id)));
    }
    
    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem>(id, token: token);
    
        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with id {id} not found.");
        }
    
        if (entity.Version != version)
        {
            throw new InvalidOperationException($"Version mismatch: expected {entity.Version}, got {version}.");
        }
    
        // Update entity from json
        var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(json);
        if (updatedEntity == null)
        {
            throw new InvalidOperationException("Deserialization failed.");
        }
        updatedEntity.Id = id;
    
        await collection.UpdateAsync(id, updatedEntity);
    }

    public Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException("StellarDB does not support transactions");
    }
}
