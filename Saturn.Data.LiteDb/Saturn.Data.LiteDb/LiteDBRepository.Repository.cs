using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IRepository
{
    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await GetCollection<TItem>().DeleteManyAsync(f => IDs.Contains(f.Id));
    }

    public virtual async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().InsertAsync(entity);
    }

    public virtual async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
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

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await Upsert(entities, cancellationToken: cancellationToken);
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
            var res = await coll.UpdateAsync(list[i]);

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

            _ = await coll.UpsertAsync(list[i]);
        }
    }

    public virtual async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await GetCollection<TItem>().DeleteManyAsync(filter);
    }

    public virtual async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        await Delete<TItem>(f => f.Id == id, cancellationToken: cancellationToken);
    }

    public virtual async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var existingEntity = await collection.FindOneAsync(e => e.Id == id);

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
        
        var result = await collection.UpdateAsync(updatedEntity);

        if (!result)
        {
            throw new FailedToUpdateException();
        }
    }
}