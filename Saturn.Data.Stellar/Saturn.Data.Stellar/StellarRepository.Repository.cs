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
        var ids = IDs.ToList();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, ids: ids, filter: item => ids.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => ids.Contains(item.Id), cancellationToken)
                : null;

            await DeleteCore<TItem>(item => ids.Contains(item.Id), token: cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, ids, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: new[] { entity }, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context);

        try
        {
            await InsertCore(entity, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var entityList = entities.ToList();

        if (entityList.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: entityList, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context);

        try
        {
            await InsertCore(entityList, token);

            var ids = entityList.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, entityList.Count, ids));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: new[] { entity }, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context);

        try
        {
            var existed = await ExistsAsync<TItem>(entity.Id);
            await UpsertCore(entity, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entities.ToList();
        await Save(entityList, token: cancellationToken);
    }

    public async Task Save<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entities.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: entities, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context);

        try
        {
            var entitiesToAdd = entities.Where(entity => string.IsNullOrEmpty(entity.Id)).ToList();

            await SaveCore(entities, token);

            var ids = entities.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, entities.Count, ids, wasCreated: entitiesToAdd.Count > 0));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: e => e.Id == entity.Id, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            await UpsertCore(entity, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, 1, new[] { entity.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: conditionPredicate, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
            var items = collection.AsQueryable().Where(conditionPredicate).ToList();

            if (items.Count == 0)
            {
                throw new FailedToUpdateException();
            }

            foreach (var item in items)
            {
                await collection.UpdateAsync(item.Id, entity);
            }

            var matchedIds = items.Select(item => item.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, items.Count, matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entities.ToList();
        await Update(entityList, token: cancellationToken);
    }

    public async Task Update<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entities.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, items: entities, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            await SaveCore(entities, token);

            var ids = entities.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, entities.Count, ids));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: new[] { entity }, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context);

        try
        {
            var existed = await ExistsAsync<TItem>(entity.Id);
            await UpsertCore(entity, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var entityList = entity.ToList();
        await Upsert(entityList, token: cancellationToken);
    }

    public async Task Upsert<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        if (entities.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: entities, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context);

        try
        {
            var anyCreated = false;
            foreach (var entityItem in entities)
            {
                if (!await ExistsAsync<TItem>(entityItem.Id))
                {
                    anyCreated = true;
                }

                await UpsertCore(entityItem, token);
            }

            var ids = entities.Select(entityItem => entityItem.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, entities.Count, ids, wasCreated: anyCreated));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, filter: filter, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, token)
                : null;

            await DeleteCore(filter, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, id: id, filter: item => item.Id == id, transaction: transaction, cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => item.Id == id, token)
                : null;

            await DeleteCore<TItem>(item => item.Id == id, token);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, new[] { id }, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, cancellationToken)
                : null;

            await HardDeleteCore(filter, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, id: id, filter: item => item.Id == id, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => item.Id == id, cancellationToken)
                : null;

            await HardDeleteCore<TItem>(item => item.Id == id, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, new[] { id }, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var ids = IDs.ToList();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, ids: ids, filter: item => ids.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => ids.Contains(item.Id), cancellationToken)
                : null;

            await HardDeleteCore<TItem>(item => ids.Contains(item.Id), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, ids, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Restore<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, id: id, filter: item => item.Id == id, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => item.Id == id, cancellationToken)
                : null;

            await RestoreCore<TItem>(item => item.Id == id, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, matchedIds?.Count ?? 0, new[] { id }, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var ids = IDs.ToList();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, ids: ids, filter: item => ids.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => ids.Contains(item.Id), cancellationToken)
                : null;

            await RestoreCore<TItem>(item => ids.Contains(item.Id), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, matchedIds?.Count ?? 0, ids, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, cancellationToken)
                : null;

            await RestoreCore(filter, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    private async Task DispatchRestoreAsync<TItem>(RepositoryWriteContext<TItem> context, Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
    {
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);
        await RestoreCore(filter, cancellationToken);
    }

    public async Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null, IDataUpdateDefinition<TItem> updateDefinition = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(jsonDocument) && updateDefinition == null)
        {
            throw new ArgumentException("At least one patch input must be supplied.", nameof(jsonDocument));
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: id, expectedVersion: expectedVersion, jsonDocument: jsonDocument,
            updateDefinition: updateDefinition, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context);

        try
        {
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

                var hasOperators = patchNode.ContainsKey("$set") || patchNode.ContainsKey("$unset") || patchNode.ContainsKey("$inc");

                if (hasOperators)
                {
                    var mergeNode = patchNode.TryGetPropertyValue("$set", out var setValue) && setValue is JsonObject setObject
                        ? setObject
                        : new JsonObject();

                    foreach (var property in mergeNode)
                    {
                        if (property.Key == nameof(Entity.Id))
                        {
                            continue;
                        }

                        existingNode[property.Key] = property.Value?.DeepClone();
                    }

                    if (patchNode.TryGetPropertyValue("$unset", out var unsetValue) && unsetValue is JsonObject unsetObject)
                    {
                        foreach (var property in unsetObject)
                        {
                            existingNode.Remove(property.Key);
                        }
                    }

                    if (patchNode.TryGetPropertyValue("$inc", out var incValue) && incValue is JsonObject incObject)
                    {
                        foreach (var property in incObject)
                        {
                            var currentValue = existingNode[property.Key]?.GetValue<decimal>() ?? 0m;
                            var deltaValue = property.Value?.GetValue<decimal>() ?? 0m;
                            existingNode[property.Key] = currentValue + deltaValue;
                        }
                    }
                }
                else
                {
                    foreach (var property in patchNode)
                    {
                        if (property.Key == nameof(Entity.Id))
                        {
                            continue;
                        }

                        existingNode[property.Key] = property.Value?.DeepClone();
                    }
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

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, 1, new[] { id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
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
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: id, expectedVersion: version, jsonDocument: json, transaction: transaction,
            cancellationToken: token);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context);

        try
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
    
            var updatedEntity = JsonSerializer.Deserialize<TItem>(json);
            if (updatedEntity == null)
            {
                throw new InvalidOperationException("Deserialization failed.");
            }
            updatedEntity.Id = id;
    
            await collection.UpdateAsync(id, updatedEntity);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, 1, new[] { id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException("StellarDB does not support transactions");
    }

    private async Task InsertCore<TItem>(TItem entity, CancellationToken token) where TItem : Entity
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    private async Task InsertCore<TItem>(List<TItem> entityList, CancellationToken token) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        foreach (var entity in entityList)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = EntityId.GenerateNewId();
            }
        }

        var entityDictionary = entityList.ToDictionary(
            entity => new EntityId(entity.Id),
            entity => entity
        );
        
        await collection.AddBulkAsync(entityDictionary);
    }

    private async Task UpsertCore<TItem>(TItem entity, CancellationToken token) where TItem : Entity
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

    private async Task SaveCore<TItem>(List<TItem> entities, CancellationToken token) where TItem : Entity
    {
        var entitiesToUpdate = entities.Where(entity => !string.IsNullOrEmpty(entity.Id)).ToList();
        var entitiesToAdd = entities.Where(entity => string.IsNullOrEmpty(entity.Id)).ToList();

        foreach (var entity in entitiesToUpdate)
        {
            await UpsertCore(entity, token);
        }

        await InsertCore(entitiesToAdd, token);
    }

    private async Task DeleteCore<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken token) where TItem : Entity
    {
        if (SupportsSoftDelete<TItem>())
        {
            await SoftDelete(filter, token);
            return;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    private async Task HardDeleteCore<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(filter).Select(item => new EntityId(item.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    private async Task RestoreCore<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
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

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Increment, id: id, expectedVersion: expectedVersion, incrementField: field,
            incrementDelta: delta, transaction: null, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Increment, context);

        try
        {
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

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, 1, new[] { existing.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
}
