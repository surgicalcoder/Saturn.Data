using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IRepository
{
    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var normalizedIds = IDs?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        if (normalizedIds.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, ids: normalizedIds, filter: item => normalizedIds.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => normalizedIds.Contains(item.Id), cancellationToken)
                : null;

            await DeleteCore<TItem>(item => normalizedIds.Contains(item.Id), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, normalizedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context);

        try
        {
            await GetCollection<TItem>().Insert(entity, ResolveLiteTransaction(transaction), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        var list = entities as IList<TItem> ?? new List<TItem>(entities);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context);

        try
        {
            await GetCollection<TItem>().Insert(list, ResolveLiteTransaction(transaction), cancellationToken);

            var ids = list.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, list.Count, ids));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context);

        try
        {
            var inserted = await GetCollection<TItem>().Upsert(entity, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: inserted));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entities == null)
        {
            return;
        }

        var list = entities as IList<TItem> ?? new List<TItem>(entities);

        if (list.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context);

        try
        {
            var anyInserted = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrEmpty(list[i].Id))
                {
                    list[i].Id = ObjectId.NewObjectId().ToString();
                }

                if (await GetCollection<TItem>().Upsert(list[i], cancellationToken))
                {
                    anyInserted = true;
                }
            }

            var ids = list.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, ids, wasCreated: anyInserted));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: e => e.Id == entity.Id, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            var updateResult = await GetCollection<TItem>().Update(entity, cancellationToken);

            if (!updateResult)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, 1, new[] { entity.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: conditionPredicate, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            var coll = GetCollection<TItem>();
            var id = await coll.FindOne(conditionPredicate, cancellationToken);

            if (id == null)
            {
                throw new FailedToUpdateException();
            }

            await coll.Update(id.Id, entity, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, 1, new[] { id.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entities == null)
        {
            return;
        }
    
        var list = entities as IList<TItem> ?? new List<TItem>(entities);
    
        if (list.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context);

        try
        {
            var coll = GetCollection<TItem>();

            for (var i = 0; i < list.Count; i++)
            {
                var res = await coll.Update(list[i], cancellationToken);

                if (!res)
                {
                    throw new FailedToUpdateException();
                }
            }

            var ids = list.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, list.Count, ids));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public virtual async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context);

        try
        {
            var inserted = await GetCollection<TItem>().Upsert(entity, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: inserted));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity
    {
        if (entity == null)
        {
            return;
        }

        var coll = GetCollection<TItem>();
        var list = entity as IList<TItem> ?? new List<TItem>(entity);

        if (list.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context);

        try
        {
            var anyInserted = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrEmpty(list[i].Id))
                {
                    list[i].Id = ObjectId.NewObjectId().ToString();
                }

                if (await coll.Upsert(list[i], cancellationToken))
                {
                    anyInserted = true;
                }
            }

            var ids = list.Select(entity => entity.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, ids, wasCreated: anyInserted));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, cancellationToken)
                : null;

            await DeleteCore<TItem>(filter, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, id: id, filter: f => f.Id == id, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(f => f.Id == id, cancellationToken)
                : null;

            await DeleteCore<TItem>(f => f.Id == id, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, new[] { id }, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    private async Task DeleteCore<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            await GetCollection<TItem>().DeleteMany(filter, cancellationToken);
            return;
        }

        var collection = GetCollection<TItem>();
        var items = await collection.Query().Where(BsonMapper.Global.GetExpression(filter)).ToEnumerable(cancellationToken).ToListAsync(cancellationToken);

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

            var updated = await collection.Update(item, cancellationToken);

            if (!updated)
            {
                throw new FailedToUpdateException();
            }
        }
    }

    public async Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, cancellationToken)
                : null;

            await GetCollection<TItem>().DeleteMany(filter, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, id: id, filter: item => item.Id == id, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => item.Id == id, cancellationToken)
                : null;

            await GetCollection<TItem>().DeleteMany(item => item.Id == id, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, new[] { id }, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var ids = IDs.ToList();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, ids: ids, filter: item => ids.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => ids.Contains(item.Id), cancellationToken)
                : null;

            await GetCollection<TItem>().DeleteMany(item => ids.Contains(item.Id), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, matchedIds?.Count ?? 0, ids, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Restore<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
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

    public async Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalizedIds = IDs?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        if (normalizedIds.Count == 0)
        {
            return;
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, ids: normalizedIds, filter: item => normalizedIds.Contains(item.Id), transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync<TItem>(item => normalizedIds.Contains(item.Id), cancellationToken)
                : null;

            await RestoreCore<TItem>(item => normalizedIds.Contains(item.Id), cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, matchedIds?.Count ?? 0, normalizedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(filter, cancellationToken)
                : null;

            await RestoreCore<TItem>(filter, cancellationToken);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, matchedIds?.Count ?? 0, entityIds: matchedIds, matchedIds: matchedIds));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    private async Task RestoreCore<TItem>(Expression<Func<TItem, bool>> filter, CancellationToken cancellationToken) where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            throw new NotSupportedException($"Type '{typeof(TItem).Name}' does not support soft delete restore.");
        }

        var collection = GetCollection<TItem>();
        var items = await collection.Query().Where(BsonMapper.Global.GetExpression(filter)).ToEnumerable(cancellationToken).ToListAsync(cancellationToken);

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

            var updated = await collection.Update(item, cancellationToken);

            if (!updated)
            {
                throw new FailedToUpdateException();
            }
        }
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
            var collection = GetCollection<TItem>();
            var existing = await collection.FindById(id, cancellationToken);

            if (existing == null)
            {
                throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
            }

            if (expectedVersion.HasValue && existing.Version != expectedVersion.Value)
            {
                throw new FailedToUpdateException();
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
                        if (property.Key == "_id")
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
                        if (property.Key == "_id")
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
                if (updateDefinition is not LiteDbDataUpdateDefinition<TItem> liteDbUpdateDefinition)
                {
                    throw new NotSupportedException($"Update definition type '{updateDefinition.GetType().Name}' is not supported by LiteDbX.");
                }

                liteDbUpdateDefinition.Apply(working);
            }

            working.Id = existing.Id;
            working.Version = (existing.Version ?? 0) + 1;

            var updated = await collection.Update(working, cancellationToken);

            if (!updated)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, 1, new[] { id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment(id, field, delta, (value, change) => value + change, expectedVersion, cancellationToken);
    }

    private async Task Increment<TItem, TNumber>(string id, Expression<Func<TItem, TNumber>> field, TNumber delta,
        Func<TNumber, TNumber, TNumber> add, long? expectedVersion, CancellationToken cancellationToken) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.Body is not MemberExpression memberExpression || memberExpression.Member is not System.Reflection.PropertyInfo propertyInfo)
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
            var collection = GetCollection<TItem>();
            var existing = await collection.FindById(id, cancellationToken);

            if (existing == null)
            {
                throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
            }

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

            var updated = await collection.Update(existing, cancellationToken);

            if (!updated)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, 1, new[] { existing.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public virtual async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: id, expectedVersion: version, jsonDocument: json, transaction: transaction,
            cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Patch, context);

        try
        {
            var collection = GetCollection<TItem>();
            
            var existingEntity = await collection.FindOne(e => e.Id == id, cancellationToken);

            if (existingEntity == null)
            {
                throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
            }
            
            var updateDoc = JsonSerializer.Deserialize<BsonDocument>(json);
            
            if (existingEntity.Version != version)
            {
                throw new ApplicationException($"Entity version mismatch. Current version: {existingEntity.Version}, requested version: {version}");
            }
            
            var updatedEntity = BsonMapper.Global.ToObject<TItem>(updateDoc);
            updatedEntity.Id = id;
            updatedEntity.Version = version + 1;
            
            var result = await collection.Update(updatedEntity, cancellationToken);

            if (!result)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, 1, new[] { id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
}
