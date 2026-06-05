using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IRepository
{
    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            await GetCollection<TItem>().DeleteMany(f => IDs.Contains(f.Id), cancellationToken);
            return;
        }

        var normalizedIds = IDs?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        if (normalizedIds.Count == 0)
        {
            return;
        }

        await Delete<TItem>(item => normalizedIds.Contains(item.Id), transaction, cancellationToken);
    }

    public virtual async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().Insert(entity, cancellationToken);
    }

    public virtual async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }
        await GetCollection<TItem>().Insert(entities, cancellationToken);
    }

    public virtual async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await Upsert(entities, cancellationToken: cancellationToken);
    }

    public virtual async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var updateResult = await GetCollection<TItem>().Update(entity, cancellationToken);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }

    public virtual async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var coll = GetCollection<TItem>();
        var id = await coll.FindOne(conditionPredicate, cancellationToken);
        await coll.Update(id.Id, entity, cancellationToken);
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

        var coll = GetCollection<TItem>();

        for (var i = 0; i < list.Count; i++)
        {
            var res = await coll.Update(list[i], cancellationToken);

            if (!res)
            {
                throw new FailedToUpdateException();
            }
        }
    }
    
    public virtual async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        _ = await GetCollection<TItem>().Upsert(entity, cancellationToken);
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

        for (var i = 0; i < list.Count; i++)
        {
            if (string.IsNullOrEmpty(list[i].Id))
            {
                list[i].Id = ObjectId.NewObjectId().ToString();
            }

            _ = await coll.Upsert(list[i], cancellationToken);
        }
    }

    public virtual async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
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

    public virtual async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await Delete<TItem>(f => f.Id == id, cancellationToken: cancellationToken);
    }

    public async Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        await GetCollection<TItem>().DeleteMany(filter, cancellationToken);
    }

    public async Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await GetCollection<TItem>().DeleteMany(item => item.Id == id, cancellationToken);
    }

    public async Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await GetCollection<TItem>().DeleteMany(item => IDs.Contains(item.Id), cancellationToken);
    }

    public async Task Restore<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await Restore<TItem>(item => item.Id == id, transaction, cancellationToken);
    }

    public async Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalizedIds = IDs?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        if (normalizedIds.Count == 0)
        {
            return;
        }

        await Restore<TItem>(item => normalizedIds.Contains(item.Id), transaction, cancellationToken);
    }

    public async Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
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

        var working = existing;

        if (!string.IsNullOrWhiteSpace(jsonDocument))
        {
            var patchDocument = JsonSerializer.Deserialize<BsonDocument>(jsonDocument);

            if (patchDocument == null)
            {
                throw new ApplicationException("Unable to deserialize patch JSON into a BSON document.");
            }

            var existingDocument = BsonMapper.Global.ToDocument(existing);

            foreach (var key in patchDocument.Keys)
            {
                if (key == "_id")
                {
                    continue;
                }

                existingDocument[key] = patchDocument[key];
            }

            working = BsonMapper.Global.ToObject<TItem>(existingDocument);
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
    }

    public virtual async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
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
    }
}