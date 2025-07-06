using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IRepository
{
    public virtual async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().InsertAsync(entity);
    }

    public virtual async Task InsertMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }
        await GetCollection<TItem>().InsertAsync(entities);
    }

    public virtual async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public virtual async Task SaveMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await UpsertMany(entities, cancellationToken: cancellationToken);
    }

    public virtual async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var updateResult = await GetCollection<TItem>().UpdateAsync(entity);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }

    public virtual async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var coll = GetCollection<TItem>();
        var id = await coll.FindOneAsync(conditionPredicate);
        await coll.UpdateAsync(id.Id, entity);
    }

    public virtual async Task UpdateMany<TItem>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (entities == null || entities.Count == 0)
        {
            return;
        }

        var coll = GetCollection<TItem>();

        for (var i = 0; i < entities.Count; i++)
        {
            var res = await coll.UpdateAsync(entities[i]);

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

        _ = await GetCollection<TItem>().UpsertAsync(entity);
    }

    public virtual async Task UpsertMany<TItem>(List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (entity == null || entity.Count == 0)
        {
            return;
        }

        var coll = GetCollection<TItem>();

        for (var i = 0; i < entity.Count; i++)
        {
            if (string.IsNullOrEmpty(entity[i].Id))
            {
                entity[i].Id = ObjectId.NewObjectId().ToString();
            }

            _ = await coll.UpsertAsync(entity[i]);
        }
    }

    public virtual async Task Delete<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await GetCollection<TItem>().DeleteManyAsync(f => f.Id == entity.Id);
    }

    public virtual async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await GetCollection<TItem>().DeleteManyAsync(filter);
    }

    public virtual async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await Delete<TItem>(f => f.Id == id, cancellationToken: cancellationToken);
    }

    public virtual async Task DeleteMany<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (!entities.Any())
        {
            return;
        }

        var list = entities.Select(r => r.Id).ToList();

        await GetCollection<TItem>().DeleteManyAsync(arg => list.Contains(arg.Id));
    }

    public virtual async Task DeleteMany<TItem>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (IDs.Count == 0)
        {
            return;
        }

        await GetCollection<TItem>().DeleteManyAsync(f => IDs.Contains(f.Id));
    }

    public virtual async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = GetCollection<TItem>();

        // Find the existing entity
        var existingEntity = await collection.FindOneAsync(e => e.Id == id);

        if (existingEntity == null)
        {
            throw new ApplicationException($"Entity of type {typeof(TItem).Name} with ID {id} was not found.");
        }

        // Parse the JSON to BsonDocument
        var updateDoc = JsonSerializer.Deserialize<BsonDocument>(json);

        // Version check for optimistic concurrency
        if (existingEntity.Version != version)
        {
            throw new ApplicationException($"Entity version mismatch. Current version: {existingEntity.Version}, requested version: {version}");
        }

        // Convert the update document to entity and preserve ID
        var updatedEntity = BsonMapper.Global.ToObject<TItem>(updateDoc);
        updatedEntity.Id = id;
        updatedEntity.Version = version + 1;

        // Update the entity
        var result = await collection.UpdateAsync(updatedEntity);

        if (!result)
        {
            throw new FailedToUpdateException();
        }
    }
}