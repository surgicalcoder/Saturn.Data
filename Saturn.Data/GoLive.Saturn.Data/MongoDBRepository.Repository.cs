using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IRepository
{
    public async Task Insert<T>(T entity) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.GenerateNewId().ToString();
        }
        await GetCollection<T>().InsertOneAsync(entity);
    }

    public async Task InsertMany<T>(IEnumerable<T> entities) where T : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        await GetCollection<T>().InsertManyAsync(entities, new InsertManyOptions() { IsOrdered = true });
    }

    public async Task Save<T>(T entity) where T : Entity
    {
        await Upsert<T>(entity);
    }

    public async Task SaveMany<T>(List<T> entities) where T : Entity
    {
        await UpsertMany<T>(entities);
    }

    public async Task Update<T>(T entity) where T : Entity
    {
        var updateResult = await GetCollection<T>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false });

        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task UpdateMany<T>(List<T> entities) where T : Entity
    {
        if (entities == null || entities.Count == 0)
        {
            return;
        }

        var writeModel = entities.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = false });

        var bulkWriteResult = await GetCollection<T>().BulkWriteAsync(writeModel, new BulkWriteOptions() { IsOrdered = false });
            
        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Upsert<T>(T entity) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.GenerateNewId().ToString();
        }
            
        var updateResult = await GetCollection<T>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true });
            
        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }

    public async Task UpsertMany<T>(List<T> entity) where T : Entity
    {
        if (entity == null || entity.Count == 0)
        {
            return;
        }

        for (int i = 0; i < entity.Count; i++)
        {
            if (string.IsNullOrEmpty(entity[i].Id))
            {
                entity[i].Id = ObjectId.GenerateNewId().ToString();
            }
        }

        var bulkWriteResult = await GetCollection<T>().BulkWriteAsync(entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions(){IsOrdered = false});

        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }

    public async Task Delete<T>(T entity) where T : Entity
    {
        await GetCollection<T>().DeleteOneAsync(f => f.Id == entity.Id);
    }

    public async Task Delete<T>(Expression<Func<T, bool>> filter) where T : Entity
    {
        await GetCollection<T>().DeleteManyAsync(filter);
    }

    public async Task Delete<T>(string id) where T : Entity
    {
        await Delete<T>(f => f.Id == id);
    }

    public async Task DeleteMany<T>(IEnumerable<T> entities) where T : Entity
    {
        if (!entities.Any())
        {
            return;
        }
        var list = entities.Select(r => r.Id).ToList();

        await GetCollection<T>().DeleteManyAsync(arg => list.Contains(arg.Id));
    }

    public async Task DeleteMany<T>(List<string> IDs) where T : Entity
    {
        if (IDs.Count == 0)
        {
            return;
        }

        await GetCollection<T>().DeleteManyAsync(f => IDs.Contains(f.Id));
    }

    public async Task JsonUpdate<T>(string id, int version, string json) where T : Entity
    {
        var updateOneAsync = await GetCollection<T>().UpdateOneAsync(e => e.Id == id && ((e.Version.HasValue && e.Version <= version ) || !e.Version.HasValue), new JsonUpdateDefinition<T>(json));

        if (!updateOneAsync.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Update<T>(Expression<Func<T, bool>> conditionPredicate, T entity) where T : Entity
    {
        var pred = conditionPredicate.And(e => e.Id == entity.Id);
        var updateResult = await GetCollection<T>().ReplaceOneAsync(pred, entity, new ReplaceOptions { IsUpsert = false });

        if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 || updateResult.ModifiedCount == 0)
        {
            throw new FailedToUpdateException();
        }
    }
    
}