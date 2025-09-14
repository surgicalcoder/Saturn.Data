using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IRepository
{
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await ExecuteWithTransaction<TItem>(
            transaction,
            (collection, session) => collection.DeleteManyAsync(session, filter, cancellationToken: cancellationToken),
            collection => collection.DeleteManyAsync(filter, cancellationToken: cancellationToken)
        );
    }
    
    public async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await ExecuteWithTransaction<TItem>(
            transaction,
            (collection, session) => collection.DeleteOneAsync(session, f => f.Id == id, cancellationToken: cancellationToken),
            collection => collection.DeleteOneAsync(f => f.Id == id, cancellationToken: cancellationToken)
        );
    }

    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await ExecuteWithTransaction<TItem>(
            transaction,
            (collection, session) => collection.DeleteManyAsync(session, e => IDs.Contains(e.Id), cancellationToken: cancellationToken),
            collection => collection.DeleteManyAsync(e => IDs.Contains(e.Id), cancellationToken: cancellationToken)
        );
    }
    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }

        await ExecuteWithTransaction<TItem>(
            transaction,
            (collection, session) => collection.InsertOneAsync(session, entity, cancellationToken: cancellationToken),
            collection => collection.InsertOneAsync(entity, cancellationToken: cancellationToken)
        );
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        var entitiesList = entities as IList<TItem> ?? entities.ToList();

        for (var i = 0; i < entitiesList.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(entitiesList[i].Id))
            {
                entitiesList[i].Id = EntityIdGenerator.GenerateNewId();
            }
        }

        await ExecuteWithTransaction<TItem>(
            transaction,
            (collection, session) => collection.InsertManyAsync(session, entitiesList, new InsertManyOptions { IsOrdered = true }, cancellationToken),
            collection => collection.InsertManyAsync(entitiesList, new InsertManyOptions { IsOrdered = true }, cancellationToken)
        );
    }
    
    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        UpdateResult updateOneAsync;

        if (transaction != null)
        {
            updateOneAsync = await GetCollection<TItem>().UpdateOneAsync(((MongoDbTransactionWrapper)transaction).Session, e => e.Id == id && ((e.Version.HasValue && e.Version <= version) || !e.Version.HasValue), new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken);
        }
        else
        {
            updateOneAsync = await GetCollection<TItem>().UpdateOneAsync(e => e.Id == id && ((e.Version.HasValue && e.Version <= version) || !e.Version.HasValue), new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken);
        }

        if (!updateOneAsync.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await Upsert(entity, transaction, cancellationToken);
    }
    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        await Upsert(entities, transaction, cancellationToken);
    }
    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (transaction != null)
        {
            var updateResult = await GetCollection<TItem>().ReplaceOneAsync(((MongoDbTransactionWrapper)transaction).Session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }
        else
        {
            var updateResult = await GetCollection<TItem>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }
    }
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var pred = conditionPredicate.And(e => e.Id == entity.Id);
        ReplaceOneResult updateResult;

        if (transaction != null)
        {
            updateResult = await GetCollection<TItem>().ReplaceOneAsync(((MongoDbTransactionWrapper)transaction).Session, pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);
        }
        else
        {
            updateResult = await GetCollection<TItem>().ReplaceOneAsync(pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken);
        }

        if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 || updateResult.ModifiedCount == 0)
        {
            throw new FailedToUpdateException();
        }
    }
    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        var writeModel = entities.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = false });

        BulkWriteResult<TItem> bulkWriteResult;

        if (transaction != null)
        {
            bulkWriteResult = await GetCollection<TItem>().BulkWriteAsync(((MongoDbTransactionWrapper)transaction).Session, writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }
        else
        {
            bulkWriteResult = await GetCollection<TItem>().BulkWriteAsync(writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }

        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }
    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }

        ReplaceOneResult updateResult;

        if (transaction != null)
        {
            updateResult = await GetCollection<TItem>().ReplaceOneAsync(((MongoDbTransactionWrapper)transaction).Session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        else
        {
            updateResult = await GetCollection<TItem>().ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        
        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }
    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entity == null || !entity.Any())
        {
            return;
        }

        var entityList = entity as IList<TItem> ?? entity.ToList();
        
        for (var i = 0; i < entityList.Count; i++)
        {
            if (string.IsNullOrEmpty(entityList[i].Id))
            {
                entityList[i].Id = EntityIdGenerator.GenerateNewId();
            }
        }

        BulkWriteResult<TItem> bulkWriteResult;

        if (transaction != null)
        {
            bulkWriteResult = await GetCollection<TItem>().BulkWriteAsync(((MongoDbTransactionWrapper)transaction).Session, entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }
        else
        {
            bulkWriteResult = await GetCollection<TItem>().BulkWriteAsync(entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }


        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }
}