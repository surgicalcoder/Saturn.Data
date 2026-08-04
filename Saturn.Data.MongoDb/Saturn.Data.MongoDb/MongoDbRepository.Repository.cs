using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using Saturn.Data.MongoDb.ExpressionRewriters;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IRepository
{
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var normalizedFilter = filter.NormalizeForRef();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, filter: normalizedFilter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Delete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(GetCollection<TItem>(), transaction, normalizedFilter, cancellationToken)
                : null;

            if (!SupportsSoftDelete<TItem>())
            {
                var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                    transaction,
                    (collection, session) => collection.DeleteManyAsync(session, normalizedFilter, cancellationToken: cancellationToken),
                    collection => collection.DeleteManyAsync(normalizedFilter, cancellationToken: cancellationToken)
                );

                await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                    BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, matchedIds: matchedIds, rawResult: deleteResult));
                return;
            }

            var update = Builders<TItem>.Update
                .Set(nameof(ISoftDeletable.IsDeleted), true)
                .Set(nameof(ISoftDeletable.DeletedAt), DateTime.UtcNow)
                .Set(nameof(ISoftDeletable.DeletedBy), string.Empty)
                .Inc("_v", 1L);

            var updateResult = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateManyAsync(session, normalizedFilter, update, cancellationToken: cancellationToken),
                collection => collection.UpdateManyAsync(normalizedFilter, update, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)updateResult.ModifiedCount, matchedIds: matchedIds, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        Expression<Func<TItem, bool>> filter = item => item.Id == id;
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, id: id, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Delete, context);

        try
        {
            if (!SupportsSoftDelete<TItem>())
            {
                var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                    transaction,
                    (collection, session) => collection.DeleteOneAsync(session, filter, cancellationToken: cancellationToken),
                    collection => collection.DeleteOneAsync(filter, cancellationToken: cancellationToken)
                );

                await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                    BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, new[] { id }, rawResult: deleteResult));
                return;
            }

            var update = Builders<TItem>.Update
                .Set(nameof(ISoftDeletable.IsDeleted), true)
                .Set(nameof(ISoftDeletable.DeletedAt), DateTime.UtcNow)
                .Set(nameof(ISoftDeletable.DeletedBy), string.Empty)
                .Inc("_v", 1L);

            var updateResult = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken),
                collection => collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)updateResult.ModifiedCount, new[] { id }, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var ids = IDs.ToList();
        Expression<Func<TItem, bool>> filter = item => ids.Contains(item.Id);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, ids: ids, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Delete, context);

        try
        {
            if (!SupportsSoftDelete<TItem>())
            {
                var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                    transaction,
                    (collection, session) => collection.DeleteManyAsync(session, filter, cancellationToken: cancellationToken),
                    collection => collection.DeleteManyAsync(filter, cancellationToken: cancellationToken)
                );

                await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                    BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, ids, rawResult: deleteResult));
                return;
            }

            var update = Builders<TItem>.Update
                .Set(nameof(ISoftDeletable.IsDeleted), true)
                .Set(nameof(ISoftDeletable.DeletedAt), DateTime.UtcNow)
                .Set(nameof(ISoftDeletable.DeletedBy), string.Empty)
                .Inc("_v", 1L);

            var updateResult = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateManyAsync(session, filter, update, cancellationToken: cancellationToken),
                collection => collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)updateResult.ModifiedCount, ids, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    public async Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var normalizedFilter = filter.NormalizeForRef();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, filter: normalizedFilter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.HardDelete, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(GetCollection<TItem>(), transaction, normalizedFilter, cancellationToken)
                : null;

            var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                transaction,
                (collection, session) => collection.DeleteManyAsync(session, normalizedFilter, cancellationToken: cancellationToken),
                collection => collection.DeleteManyAsync(normalizedFilter, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, matchedIds: matchedIds, rawResult: deleteResult));
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
        Expression<Func<TItem, bool>> filter = item => item.Id == id;
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, id: id, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.HardDelete, context);

        try
        {
            var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                transaction,
                (collection, session) => collection.DeleteOneAsync(session, filter, cancellationToken: cancellationToken),
                collection => collection.DeleteOneAsync(filter, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, new[] { id }, rawResult: deleteResult));
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
        Expression<Func<TItem, bool>> filter = item => ids.Contains(item.Id);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, ids: ids, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.HardDelete, context);

        try
        {
            var deleteResult = await ExecuteWithTransaction<TItem, DeleteResult>(
                transaction,
                (collection, session) => collection.DeleteManyAsync(session, filter, cancellationToken: cancellationToken),
                collection => collection.DeleteManyAsync(filter, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, (int)deleteResult.DeletedCount, ids, rawResult: deleteResult));
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
        await Restore<TItem>(item => item.Id == id, transaction, cancellationToken);
    }

    public async Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await Restore<TItem>(item => IDs.Contains(item.Id), transaction, cancellationToken);
    }

    public async Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            throw new NotSupportedException($"Type '{typeof(TItem).Name}' does not support soft delete restore.");
        }

        var normalizedFilter = filter.NormalizeForRef();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, filter: normalizedFilter, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Restore, context);

        try
        {
            IReadOnlyList<string>? matchedIds = HasWriteBehaviors
                ? await MaterializeIdsAsync(GetCollection<TItem>(), transaction, normalizedFilter, cancellationToken)
                : null;

            var update = Builders<TItem>.Update
                .Set(nameof(ISoftDeletable.IsDeleted), false)
                .Set(nameof(ISoftDeletable.DeletedAt), (DateTime?)null)
                .Set(nameof(ISoftDeletable.DeletedBy), string.Empty)
                .Inc("_v", 1L);

            var updateResult = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateManyAsync(session, normalizedFilter, update, cancellationToken: cancellationToken),
                collection => collection.UpdateManyAsync(normalizedFilter, update, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, (int)updateResult.ModifiedCount, entityIds: matchedIds, matchedIds: matchedIds, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
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
        await ApplyWriteBehaviors(RepositoryWriteOperation.Patch, context);

        try
        {
            var updates = new List<UpdateDefinition<TItem>>();

            if (!string.IsNullOrWhiteSpace(jsonDocument))
            {
                updates.Add(new JsonUpdateDefinition<TItem>(jsonDocument));
            }

            if (updateDefinition != null)
            {
                if (updateDefinition is not MongoDataUpdateDefinition<TItem> mongoDataUpdateDefinition)
                {
                    throw new NotSupportedException($"Update definition type '{updateDefinition.GetType().Name}' is not supported by MongoDbRepository.");
                }

                updates.Add(mongoDataUpdateDefinition.Definition);
            }

            updates.Add(Builders<TItem>.Update.Inc("_v", 1L));

            FilterDefinition<TItem> filter = expectedVersion.HasValue
                ? Builders<TItem>.Filter.Where(item => item.Id == id && item.Version == expectedVersion.Value)
                : Builders<TItem>.Filter.Where(item => item.Id == id);

            var combined = Builders<TItem>.Update.Combine(updates);

            var result = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateOneAsync(session, filter, combined, cancellationToken: cancellationToken),
                collection => collection.UpdateOneAsync(filter, combined, cancellationToken: cancellationToken)
            );

            if (!result.IsAcknowledged || result.MatchedCount == 0 || result.ModifiedCount == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, (int)result.ModifiedCount, new[] { id }, rawResult: result));
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
        return Increment<TItem, int>(id, field, delta, expectedVersion, transaction, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment<TItem, long>(id, field, delta, expectedVersion, transaction, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment<TItem, double>(id, field, delta, expectedVersion, transaction, cancellationToken);
    }

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Increment<TItem, decimal>(id, field, delta, expectedVersion, transaction, cancellationToken);
    }

    private async Task Increment<TItem, TNumber>(string id, Expression<Func<TItem, TNumber>> field, TNumber delta, long? expectedVersion,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Increment, id: id, expectedVersion: expectedVersion, incrementField: field,
            incrementDelta: delta, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Increment, context);

        try
        {
            FilterDefinition<TItem> filter = expectedVersion.HasValue
                ? Builders<TItem>.Filter.Where(item => item.Id == id && item.Version == expectedVersion.Value)
                : Builders<TItem>.Filter.Where(item => item.Id == id);

            var update = Builders<TItem>.Update.Combine(
                Builders<TItem>.Update.Inc(field, delta),
                Builders<TItem>.Update.Inc("_v", 1L));

            var result = await ExecuteWithTransaction<TItem, UpdateResult>(
                transaction,
                (collection, session) => collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken),
                collection => collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            );

            if (!result.IsAcknowledged || result.MatchedCount == 0 || result.ModifiedCount == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Increment, context,
                BuildWriteResult(context, WriteOutcome.Incremented, (int)result.ModifiedCount, new[] { id }, rawResult: result));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }

    private static bool SupportsSoftDelete<TItem>() where TItem : Entity
    {
        return typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));
    }
    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Insert, context);

        try
        {
            await ExecuteWithTransaction<TItem>(
                transaction,
                (collection, session) => collection.InsertOneAsync(session, entity, cancellationToken: cancellationToken),
                collection => collection.InsertOneAsync(entity, cancellationToken: cancellationToken)
            );

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id }));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
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

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: entitiesList, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Insert, context);

        try
        {
            await ExecuteWithTransaction<TItem>(
                transaction,
                (collection, session) => collection.InsertManyAsync(session, entitiesList, new InsertManyOptions { IsOrdered = true }, cancellationToken),
                collection => collection.InsertManyAsync(entitiesList, new InsertManyOptions { IsOrdered = true }, cancellationToken)
            );

            var ids = entitiesList.Select(e => e.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, entitiesList.Count, ids));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: id, expectedVersion: version, jsonDocument: json, transaction: transaction,
            cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Patch, context);

        try
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

            await ApplyAfterBehaviors(RepositoryWriteOperation.Patch, context,
                BuildWriteResult(context, WriteOutcome.Patched, (int)result.ModifiedCount, new[] { id }, rawResult: result));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Save, context);

        try
        {
            var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
                transaction,
                (collection, session) => collection.ReplaceOneAsync(session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken),
                collection => collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken)
            );

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: updateResult.UpsertedId != null, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        var entityList = entities as IList<TItem> ?? entities.ToList();

        for (var i = 0; i < entityList.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(entityList[i].Id))
            {
                entityList[i].Id = EntityIdGenerator.GenerateNewId();
            }
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: entityList, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Save, context);

        try
        {
            var bulkWriteResult = await ExecuteWithTransaction<TItem, BulkWriteResult<TItem>>(
                transaction,
                (collection, session) => collection.BulkWriteAsync(session, entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken),
                collection => collection.BulkWriteAsync(entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken)
            );

            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }

            var ids = entityList.Select(e => e.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, (int)(bulkWriteResult.ModifiedCount + bulkWriteResult.Upserts.Count), ids,
                    wasCreated: bulkWriteResult.Upserts.Count > 0, rawResult: bulkWriteResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: e => e.Id == entity.Id,
            transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Update, context);

        try
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

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, (int)updateResult.ModifiedCount, new[] { entity.Id }, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var pred = conditionPredicate.NormalizeForRef().And(e => e.Id == entity.Id);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, id: entity.Id, items: new[] { entity }, filter: pred, transaction: transaction,
            cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Update, context);

        try
        {
            var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
                transaction,
                (collection, session) => collection.ReplaceOneAsync(session, pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken),
                collection => collection.ReplaceOneAsync(pred, entity, new ReplaceOptions { IsUpsert = false }, cancellationToken)
            );

            if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 || updateResult.ModifiedCount == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, (int)updateResult.ModifiedCount, new[] { entity.Id }, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        var entityList = entities as IList<TItem> ?? entities.ToList();
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, items: entityList, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Update, context);

        try
        {
            var writeModel = entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = false });

            var bulkWriteResult = await ExecuteWithTransaction<TItem, BulkWriteResult<TItem>>(
                transaction,
                (collection, session) => collection.BulkWriteAsync(session, writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken),
                collection => collection.BulkWriteAsync(writeModel, new BulkWriteOptions { IsOrdered = false }, cancellationToken)
            );

            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }

            var ids = entityList.Select(e => e.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, (int)bulkWriteResult.ModifiedCount, ids, rawResult: bulkWriteResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: new[] { entity }, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Upsert, context);

        try
        {
            var updateResult = await ExecuteWithTransaction<TItem, ReplaceOneResult>(
                transaction,
                (collection, session) => collection.ReplaceOneAsync(session, e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken),
                collection => collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken)
            );
        
            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: updateResult.UpsertedId != null, rawResult: updateResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
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

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: entityList, transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Upsert, context);

        try
        {
            var bulkWriteResult = await ExecuteWithTransaction<TItem, BulkWriteResult<TItem>>(
                transaction,
                (collection, session) => collection.BulkWriteAsync(session, entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken),
                collection => collection.BulkWriteAsync(entityList.Select(f => new ReplaceOneModel<TItem>(new ExpressionFilterDefinition<TItem>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions { IsOrdered = false }, cancellationToken)
            );

            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }

            var ids = entityList.Select(e => e.Id).ToList();

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, (int)(bulkWriteResult.ModifiedCount + bulkWriteResult.Upserts.Count), ids,
                    wasCreated: bulkWriteResult.Upserts.Count > 0, rawResult: bulkWriteResult));
        }
        catch (Exception ex)
        {
            await ApplyOnWriteFailed(context, ex);
            throw;
        }
    }
}
