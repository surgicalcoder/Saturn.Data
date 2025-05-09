using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IRepository
{
    public async Task Insert<T>(T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.GenerateNewId().ToString();
        }

        if (transaction != null)
        {
            await GetCollection<T>().InsertOneAsync(((MongoDBTransactionWrapper)transaction).Session, entity, cancellationToken: token);
        }
        else
        {
            await GetCollection<T>().InsertOneAsync(entity, cancellationToken: token);
        }
    }   

    public async Task InsertMany<T>(IEnumerable<T> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        if (transaction != null)
        {
            await GetCollection<T>().InsertManyAsync(((MongoDBTransactionWrapper)transaction).Session, entities, new InsertManyOptions() { IsOrdered = true }, token);
        }
        else
        {
            await GetCollection<T>().InsertManyAsync(entities, new InsertManyOptions() { IsOrdered = true }, token);
        }
    }

    public async Task Save<T>(T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        await Upsert<T>(entity, transaction: transaction, token: token);
    }

    public async Task SaveMany<T>(List<T> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        await UpsertMany<T>(entities, transaction: transaction, token: token);
    }


    public async Task Update<T>(T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (transaction != null)
        {
            var updateResult = await GetCollection<T>().ReplaceOneAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, token);
            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }
        else
        {
            var updateResult = await GetCollection<T>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, token);
            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }
    }

    public async Task UpdateMany<T>(List<T> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (entities == null || entities.Count == 0)
        {
            return;
        }

        var writeModel = entities.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = false });

        BulkWriteResult<T> bulkWriteResult;

        if (transaction != null)
        {
            bulkWriteResult = await GetCollection<T>().BulkWriteAsync(((MongoDBTransactionWrapper)transaction).Session, writeModel, new BulkWriteOptions() { IsOrdered = false }, token);
        }
        else
        {
            bulkWriteResult = await GetCollection<T>().BulkWriteAsync(writeModel, new BulkWriteOptions() { IsOrdered = false }, token);
        }

        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Upsert<T>(T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.GenerateNewId().ToString();
        }

        ReplaceOneResult updateResult;
        
        if (transaction != null)
        {
            updateResult = await GetCollection<T>().ReplaceOneAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken: token);
        }
        else
        {
            updateResult = await GetCollection<T>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken: token);
        }
            
        
        

        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }

    public async Task UpsertMany<T>(List<T> entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
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

        BulkWriteResult<T> bulkWriteResult;
        
        if (transaction != null)
        {
            bulkWriteResult = await GetCollection<T>().BulkWriteAsync(((MongoDBTransactionWrapper)transaction).Session,entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions(){IsOrdered = false}, token);
        }
        else
        {
            bulkWriteResult = await GetCollection<T>().BulkWriteAsync(entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions(){IsOrdered = false}, token);
        }

        
        
        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }


    public async Task Delete<T>(T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (transaction != null)
        {
            await GetCollection<T>().DeleteOneAsync(((MongoDBTransactionWrapper)transaction).Session, f => f.Id == entity.Id, cancellationToken: token);
        }
        else
        {
            await GetCollection<T>().DeleteOneAsync(f => f.Id == entity.Id, token);
        }
    }

    public async Task Delete<T>(Expression<Func<T, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (transaction != null)
        {
            await GetCollection<T>().DeleteManyAsync(((MongoDBTransactionWrapper)transaction).Session, filter, cancellationToken: token);
        }
        else
        {
            await GetCollection<T>().DeleteManyAsync(filter, token);
        }
    }

    public async Task Delete<T>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        await Delete<T>(f => f.Id == id, transaction, token);
    }

    public async Task DeleteMany<T>(IEnumerable<T> entities, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        if (!entities.Any())
        {
            return;
        }

        var list = entities.Select(r => r.Id).ToList();

        if (transaction != null)
        {
            await GetCollection<T>().DeleteManyAsync(((MongoDBTransactionWrapper)transaction).Session, arg => list.Contains(arg.Id), cancellationToken: token);
        }
        else
        {
            await GetCollection<T>().DeleteManyAsync(arg => list.Contains(arg.Id), token);
        }
    }

    public async Task DeleteMany<T>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
        {
            if (IDs.Count == 0)
            {
                return;
            }

            if (transaction != null)
            {
                await GetCollection<T>().DeleteManyAsync(((MongoDBTransactionWrapper)transaction).Session, f => IDs.Contains(f.Id), cancellationToken: token);
            }
            else
            {
                await GetCollection<T>().DeleteManyAsync(f => IDs.Contains(f.Id), token);
            }
        }

    public async Task JsonUpdate<T>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        UpdateResult updateOneAsync;
        
        if (transaction != null)
        {
            updateOneAsync = await GetCollection<T>().UpdateOneAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == id && ((e.Version.HasValue && e.Version <= version ) || !e.Version.HasValue), new JsonUpdateDefinition<T>(json), cancellationToken: token);
        }
        else
        {
            updateOneAsync = await GetCollection<T>().UpdateOneAsync(e => e.Id == id && ((e.Version.HasValue && e.Version <= version ) || !e.Version.HasValue), new JsonUpdateDefinition<T>(json), cancellationToken: token);
        }

        if (!updateOneAsync.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Update<T>(Expression<Func<T, bool>> conditionPredicate, T entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where T : Entity
    {
        var pred = conditionPredicate.And(e => e.Id == entity.Id);
        ReplaceOneResult updateResult;
        
        if (transaction != null)
        {
            updateResult = await GetCollection<T>().ReplaceOneAsync(((MongoDBTransactionWrapper)transaction).Session, pred, entity, new ReplaceOptions { IsUpsert = false }, token);
        }
        else
        {
            updateResult = await GetCollection<T>().ReplaceOneAsync(pred, entity, new ReplaceOptions { IsUpsert = false }, token);
        }

        if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 || updateResult.ModifiedCount == 0)
        {
            throw new FailedToUpdateException();
        }
    }
    
}