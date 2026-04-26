using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IWeakScopedRepository
{
    public async Task Delete<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope).Compile();
        var validIds = new List<EntityId>();

        foreach (var id in IDs)
        {
            var entity = collection.AsQueryable().FirstOrDefault(e => e.Id == id);
            if (entity != null && scopePredicate(entity))
            {
                validIds.Add(new EntityId(id));
            }
        }

        await collection.RemoveBulkAsync(validIds);
    }

    public async Task Insert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        ScopeModelHelper.SetScope(entity, scope);
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task Insert<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entities.Select(e =>
            {
                ScopeModelHelper.SetScope(e, scope);
                if (string.IsNullOrWhiteSpace(e.Id))
                {
                    e.Id = EntityId.GenerateNewId();
                }

                return e;
            })
            .ToDictionary(entity => string.IsNullOrEmpty(entity.Id) ? new EntityId() : new EntityId(entity.Id), entity => entity);
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task Save<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        await Upsert(scope, entities, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }

    public async Task Update<TItem>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var itemsToUpdate = collection.AsQueryable().Where(scopePredicate).Where(conditionPredicate).ToList();

        var updateTasks = itemsToUpdate.Select(async item =>
        {
            var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(System.Text.Json.JsonSerializer.Serialize(entity));
            ScopeModelHelper.SetScope(updatedEntity, scope);
            updatedEntity.Id = item.Id;
            await collection.UpdateAsync(item.Id, updatedEntity);
        });

        await Task.WhenAll(updateTasks);
    }

    public async Task Update<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        var updateTasks = entities.Select(async entity =>
        {
            ScopeModelHelper.SetScope(entity, scope);
            await collection.UpdateAsync(entity.Id, entity);
        });

        await Task.WhenAll(updateTasks);
    }

    public async Task JsonUpdate<TItem>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem>(scope, id, transaction, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with id '{id}' not found.");
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
        ScopeModelHelper.SetScope(updatedEntity, scope);

        await collection.UpdateAsync(id, updatedEntity);
    }

    public async Task Save<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        await Upsert(scope, entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        ScopeModelHelper.SetScope(entity, scope);
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

    public async Task Upsert<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        foreach (var item in entity)
        {
            await Upsert(scope, item, transaction, cancellationToken);
        }
    }

    public async Task Delete<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem>(scope, id, transaction, cancellationToken);
        if (entity != null)
        {
            await collection.RemoveAsync(id);
        }
    }

    public async Task Delete<TItem>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var items = collection.AsQueryable().Where(scopePredicate).Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }
}

