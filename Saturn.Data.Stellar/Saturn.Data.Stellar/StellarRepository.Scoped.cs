using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IScopedRepository
{
    public async Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.Scope = scope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task InsertMany<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entities.Select(e => { e.Scope = scope; return e; })
            .ToDictionary(entity => string.IsNullOrEmpty(entity.Id) ? new EntityId() : new EntityId(entity.Id), entity => entity);
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.Scope = scope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }

    public async Task UpdateMany<TItem, TScope>(string scope, List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        foreach (var entity in entities)
        {
            entity.Scope = scope;
            await collection.UpdateAsync(entity.Id, entity);
        }
    }

    public async Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
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

    public async Task UpsertMany<TItem, TScope>(string scope, List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        foreach (var entity in entities)
        {
            await Upsert<TItem, TScope>(scope, entity);
        }
    }

    public async Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveAsync(id);
    }

    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(e => e.Scope == scope).Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    public async Task DeleteMany<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(entities.Select(e => new EntityId(e.Id)));
    }

    public async Task DeleteMany<TItem, TScope>(string scope, List<string> ds, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.RemoveBulkAsync(ds.Select(id => new EntityId(id)));
    }
}