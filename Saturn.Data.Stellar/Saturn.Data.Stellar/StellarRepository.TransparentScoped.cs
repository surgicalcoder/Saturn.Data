using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ITransparentScopedRepository
{
    public async Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task InsertMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var entityDictionary = entities.ToDictionary(
            entity => string.IsNullOrEmpty(entity.Id) ? new EntityId() : new EntityId(entity.Id),
            entity => entity
        );
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        if (string.IsNullOrEmpty(entity.Id))
        {
            await collection.AddAsync(entity.Id, entity);
        }
        else
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
    }

    public async Task SaveMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var entitiesToUpdate = entities.Where(entity => !string.IsNullOrEmpty(entity.Id)).ToList();
        var entitiesToAdd = entities.Where(entity => string.IsNullOrEmpty(entity.Id)).ToList();
        await UpdateMany<TItem, TParent>(entitiesToUpdate, token: token);
        await InsertMany<TItem, TParent>(entitiesToAdd, token: token);
    }

    public async Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        await Save<TItem, TParent>(entity, token: token);
    }

    public async Task UpdateMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        await SaveMany<TItem, TParent>(entities, token: token);
    }

    public async Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        if (collection.ContainsKey(entity.Id))
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
        else
        {
            await collection.AddAsync(entity.Id, entity);
        }
    }

    public async Task Delete<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.RemoveAsync(entity.Id);
    }

    public async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    public async Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.RemoveAsync(id);
    }

    public async Task DeleteMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(entities.Select(e => new EntityId(e.Id)));
    }

    public async Task DeleteMany<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(IDs.Select(id => new EntityId(id)));
    }

    public async Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }

    async Task ITransparentScopedRepository.UpsertMany<TItem, TParent>(List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken token = default)
    {
        foreach (var item in entity)
        {
            await Upsert<TItem, TParent>(item, token: token);
        }
    }
}
