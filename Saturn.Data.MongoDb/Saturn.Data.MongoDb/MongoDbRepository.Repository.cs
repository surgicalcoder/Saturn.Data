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
        var result = await ExecuteWithTransaction<TItem, UpdateResult>(
            transaction,
            (collection, session) => collection.UpdateOneAsync(session, e => e.Id == id && ((e.Version.HasValue && e.Version <= version) || !e.Version.HasValue), new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken),
            collection => collection.UpdateOneAsync(e => e.Id == id && ((e.Version.HasValue && e.Version <= version) || !e.Version.HasValue), new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken)
        );

        if (!result.IsAcknowledged)
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
        var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
            transaction,
            (collection, session) => collection.ReplaceOneAsync(session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken),
            collection => collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken)
        );

        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var pred = conditionPredicate.And(e => e.Id == entity.Id);

        var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
            transaction,
            (collection, session) => collection.ReplaceOneAsync(session, pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken),
            collection => collection.ReplaceOneAsync(pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken)
        );

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

        var bulkWriteResult = await ExecuteWithTransaction<TItem, BulkWriteResult<TItem>>(
            transaction,
            (collection, session) => collection.BulkWriteAsync(session, writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken),
            collection => collection.BulkWriteAsync(writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken)
        );

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

        var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
            transaction,
            (collection, session) => collection.ReplaceOneAsync(session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken),
            collection => collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken)
        );
        
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

        var bulkWriteResult = await ExecuteWithTransaction<TItem, BulkWriteResult<TItem>>(
            transaction,
            (collection, session) => collection.BulkWriteAsync(session, entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken),
            collection => collection.BulkWriteAsync(entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken)
        );

        if (!bulkWriteResult.IsAcknowledged)
        {
            throw new FailedToUpsertException();
        }
    }
}