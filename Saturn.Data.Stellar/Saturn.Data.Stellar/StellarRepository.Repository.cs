using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IRepository
{
    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task InsertMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
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
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        if (entity.Id == null)
        {
            await collection.AddAsync(entity.Id, entity);
        }
        else
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
    }
    
    public async Task SaveMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
      var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
      
      var entitiesToUpdate = entities.Where(entity => !string.IsNullOrEmpty(entity.Id)).ToList();
      var entitiesToAdd = entities.Where(entity => string.IsNullOrEmpty(entity.Id)).ToList();

      await UpdateMany(entitiesToUpdate, token: token);
      await InsertMany(entitiesToAdd, token: token);

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
    
    public async Task UpdateMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        await SaveMany(entities, token: token);
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
    
    public async Task UpsertMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
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
        throw new NotImplementedException();
    }

    public Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException("StellarDB does not support transactions");
    }
}
