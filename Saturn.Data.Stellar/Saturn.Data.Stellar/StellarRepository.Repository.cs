using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IRepository
{
    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await Delete<TItem>(item => IDs.Contains(item.Id), transaction, cancellationToken);
    }

    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }
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
        
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }
        
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
    
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (SupportsSoftDelete<TItem>())
        {
            await SoftDelete(filter, token);
            return;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(r=>new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }
    
    public async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        await Delete<TItem>(item => item.Id == id, transaction, token);
    }

    public async Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(item => new EntityId(item.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    public Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return HardDelete<TItem>(item => item.Id == id, transaction, cancellationToken);
    }

    public Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return HardDelete<TItem>(item => IDs.Contains(item.Id), transaction, cancellationToken);
    }

    public Task Restore<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Restore<TItem>(item => item.Id == id, transaction, cancellationToken);
    }

    public Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Restore<TItem>(item => IDs.Contains(item.Id), transaction, cancellationToken);
    }

    public async Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            throw new NotSupportedException($"Type '{typeof(TItem).Name}' does not support soft delete restore.");
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).ToList();

        foreach (var item in items)
        {
            if (item is not ISoftDeletable softDeletable)
            {
                continue;
            }

            softDeletable.IsDeleted = false;
            softDeletable.DeletedAt = null;
            softDeletable.DeletedBy = string.Empty;
            item.Version = (item.Version ?? 0) + 1;
            await collection.UpdateAsync(item.Id, item);
        }
    }

    public async Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null, IDataUpdateDefinition<TItem> updateDefinition = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(jsonDocument) && updateDefinition == null)
        {
            throw new ArgumentException("At least one patch input must be supplied.", nameof(jsonDocument));
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        if (!collection.ContainsKey(id))
        {
            throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
        }

        var existing = collection[id];

        if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
        {
            throw new ApplicationException($"Entity version mismatch. Current version: {existing.Version}, requested version: {expectedVersion.Value}");
        }

        var working = existing;

        if (!string.IsNullOrWhiteSpace(jsonDocument))
        {
            var existingNode = JsonNode.Parse(JsonSerializer.Serialize(existing)) as JsonObject;
            var patchNode = JsonNode.Parse(jsonDocument) as JsonObject;

            if (existingNode == null || patchNode == null)
            {
                throw new ApplicationException("Patch JSON must be a JSON object.");
            }

            foreach (var property in patchNode)
            {
                if (property.Key == nameof(Entity.Id))
                {
                    continue;
                }

                existingNode[property.Key] = property.Value?.DeepClone();
            }

            working = existingNode.Deserialize<TItem>();

            if (working == null)
            {
                throw new ApplicationException("Deserialization failed.");
            }
        }

        if (updateDefinition != null)
        {
            if (updateDefinition is not StellarDataUpdateDefinition<TItem> stellarUpdateDefinition)
            {
                throw new NotSupportedException($"Update definition type '{updateDefinition.GetType().Name}' is not supported by StellarRepository.");
            }

            stellarUpdateDefinition.Apply(working);
        }

        working.Id = existing.Id;
        working.Version = (existing.Version ?? 0) + 1;

        await collection.UpdateAsync(working.Id, working);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
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

    private async Task SoftDelete<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).ToList();

        foreach (var item in items)
        {
            if (item is not ISoftDeletable softDeletable)
            {
                continue;
            }

            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = DateTime.UtcNow;
            softDeletable.DeletedBy = string.Empty;
            item.Version = (item.Version ?? 0) + 1;
            await collection.UpdateAsync(item.Id, item);
        }
    }

    private async Task Increment<TItem, TNumber>(string id, Expression<Func<TItem, TNumber>> field, TNumber delta,
        Func<TNumber, TNumber, TNumber> add, long? expectedVersion, CancellationToken cancellationToken) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.Body is not MemberExpression memberExpression || memberExpression.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException("Increment field must target a writable property.", nameof(field));
        }

        if (!propertyInfo.CanRead || !propertyInfo.CanWrite)
        {
            throw new ArgumentException("Increment field must target a readable and writable property.", nameof(field));
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        if (!collection.ContainsKey(id))
        {
            throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
        }

        var existing = collection[id];

        if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
        {
            throw new ApplicationException($"Entity version mismatch. Current version: {existing.Version}, requested version: {expectedVersion.Value}");
        }

        var current = propertyInfo.GetValue(existing);

        if (current is not TNumber currentValue)
        {
            throw new ApplicationException($"Field '{propertyInfo.Name}' value type does not match increment type '{typeof(TNumber).Name}'.");
        }

        propertyInfo.SetValue(existing, add(currentValue, delta));
        existing.Version = (existing.Version ?? 0) + 1;

        await collection.UpdateAsync(existing.Id, existing);
    }
}
