using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IRepository
{
    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);

        var context = BuildWriteContext(RepositoryWriteOperation.Insert, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await store.Insert(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Insert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await store.BatchInsert(list).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);

        var context = BuildWriteContext(RepositoryWriteOperation.Save, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var existed = await ExistsAsync<TItem>(entity.Id, cancellationToken).ConfigureAwait(false);
            await store.Upsert(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Save, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await store.BatchUpsert(list).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);

        var context = BuildWriteContext(RepositoryWriteOperation.Upsert, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var existed = await ExistsAsync<TItem>(entity.Id, cancellationToken).ConfigureAwait(false);
            await store.Upsert(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entity.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Upsert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await store.BatchUpsert(list).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, list.Select(item => item.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);

        var context = BuildWriteContext(RepositoryWriteOperation.Update, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var existed = await ExistsAsync<TItem>(entity.Id, cancellationToken).ConfigureAwait(false);

            if (!existed)
            {
                throw new FailedToUpdateException();
            }

            await store.Update(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, 1, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);

        var combined = Query.PredicateComposer.AndAlso(conditionPredicate, item => item.Id == entity.Id);
        var context = BuildWriteContext(RepositoryWriteOperation.Update, item: entity, filter: conditionPredicate, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var matches = (await store.Query<TItem>().Where(combined).ToList().ConfigureAwait(false)).ToList();

            if (matches.Count == 0)
            {
                throw new FailedToUpdateException();
            }

            await store.Update(entity).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, 1, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext(RepositoryWriteOperation.Update, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await store.BatchUpdate(list).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => DeleteCore(filter, transaction, cancellationToken);

    public Task Delete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);

        return normalized is null ? Task.CompletedTask : DeleteCore<TItem>(item => item.Id == normalized, transaction, cancellationToken);
    }

    public Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        return normalized.Count == 0 ? Task.CompletedTask : DeleteCore<TItem>(item => normalized.Contains(item.Id), transaction, cancellationToken);
    }

    private async Task DeleteCore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        var context = BuildWriteContext(RepositoryWriteOperation.Delete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var ids = await MatchingIdsAsync<TItem>(filter, cancellationToken).ConfigureAwait(false);

            if (SupportsSoftDelete<TItem>())
            {
                foreach (var id in ids)
                {
                    var entity = await store.Get<TItem>(id).ConfigureAwait(false);

                    if (entity is not ISoftDeletable deletable)
                    {
                        continue;
                    }

                    deletable.IsDeleted = true;
                    deletable.DeletedAt = DateTime.UtcNow;
                    deletable.DeletedBy = string.Empty;

                    await store.Update(entity).ConfigureAwait(false);
                }
            }
            else if (ids.Count > 0)
            {
                await store.BatchRemove<TItem>(ids).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Delete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, ids.Count, ids)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => HardDeleteCore(filter, transaction, cancellationToken);

    public Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);

        return normalized is null ? Task.CompletedTask : HardDeleteCore<TItem>(item => item.Id == normalized, transaction, cancellationToken);
    }

    public Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        return normalized.Count == 0 ? Task.CompletedTask : HardDeleteCore<TItem>(item => normalized.Contains(item.Id), transaction, cancellationToken);
    }

    private async Task HardDeleteCore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        var context = BuildWriteContext(RepositoryWriteOperation.HardDelete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var ids = await MatchingIdsAsync<TItem>(filter, cancellationToken).ConfigureAwait(false);

            if (ids.Count > 0)
            {
                await store.BatchRemove<TItem>(ids).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, ids.Count, ids)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task Restore<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);

        return normalized is null ? Task.CompletedTask : RestoreCore<TItem>(item => item.Id == normalized, transaction, cancellationToken);
    }

    public Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        return normalized.Count == 0 ? Task.CompletedTask : RestoreCore<TItem>(item => normalized.Contains(item.Id), transaction, cancellationToken);
    }

    public Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => RestoreCore(filter, transaction, cancellationToken);

    private async Task RestoreCore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            throw new NotSupportedException($"Restore is not supported for non-soft-deletable entity '{typeof(TItem).Name}'.");
        }

        var context = BuildWriteContext(RepositoryWriteOperation.Restore, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            var ids = await MatchingIdsAsync<TItem>(filter, cancellationToken).ConfigureAwait(false);

            foreach (var id in ids)
            {
                var entity = await store.Get<TItem>(id).ConfigureAwait(false);

                if (entity is not ISoftDeletable deletable)
                {
                    continue;
                }

                deletable.IsDeleted = false;
                deletable.DeletedAt = null;
                deletable.DeletedBy = null;

                await store.Update(entity).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Restore, context,
                BuildWriteResult(context, WriteOutcome.Restored, ids.Count, ids)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task<IDatabaseTransaction> CreateTransaction()
        => throw new NotSupportedException("Transactions are implemented in Phase 5.");

    public Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("JsonUpdate is implemented in Phase 4.");

    public Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null, IDataUpdateDefinition<TItem> updateDefinition = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Patch is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task<CascadeReport> DeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Cascade is implemented in Phase 6.");

    public Task<CascadeReport> HardDeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Cascade is implemented in Phase 6.");
}
