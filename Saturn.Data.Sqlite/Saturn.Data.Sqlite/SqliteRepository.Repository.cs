using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Query;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IRepository
{
    private const string InsertSqlTemplate = """
        INSERT INTO {0} (_id,_v,_deleted,_scope,_scope2,_archived,_doc)
        VALUES (@id, json_extract(@doc,'$.Version'), COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                json_extract(@doc,'$.Scope'), json_extract(@doc,'$.SecondScope'),
                COALESCE(json_extract(@doc,'$.IsArchived'),0), @doc);
        """;

    private const string UpsertSqlTemplate = """
        INSERT INTO {0} (_id,_v,_deleted,_scope,_scope2,_archived,_doc)
        VALUES (@id, json_extract(@doc,'$.Version'), COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                json_extract(@doc,'$.Scope'), json_extract(@doc,'$.SecondScope'),
                COALESCE(json_extract(@doc,'$.IsArchived'),0), @doc)
        ON CONFLICT(_id) DO UPDATE SET
            _doc = excluded._doc,
            _v = excluded._v,
            _deleted = excluded._deleted,
            _scope = excluded._scope,
            _scope2 = excluded._scope2,
            _archived = excluded._archived;
        """;

    private async Task<int> ExecuteInsertAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(InsertSqlTemplate, Quote(GetCollectionNameForType<TItem>()));
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUpsertAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(UpsertSqlTemplate, Quote(GetCollectionNameForType<TItem>()));
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUpdateByIdAsync<TItem>(SqliteConnection connection, TItem entity, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = @doc,
                _v = json_extract(@doc,'$.Version'),
                _deleted = COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                _scope = json_extract(@doc,'$.Scope'),
                _scope2 = json_extract(@doc,'$.SecondScope'),
                _archived = COALESCE(json_extract(@doc,'$.IsArchived'),0)
            WHERE _id = @id;
            """;
        command.Parameters.AddWithValue("@id", entity.Id);
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteUpdateWhereAsync<TItem>(SqliteConnection connection, TItem entity, SqlFragment predicate, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = @doc,
                _v = json_extract(@doc,'$.Version'),
                _deleted = COALESCE(json_extract(@doc,'$.IsDeleted'),0),
                _scope = json_extract(@doc,'$.Scope'),
                _scope2 = json_extract(@doc,'$.SecondScope'),
                _archived = COALESCE(json_extract(@doc,'$.IsArchived'),0)
            WHERE {predicate.Sql};
            """;
        command.Parameters.AddWithValue("@doc", serializer.Serialize(entity));

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ExistsByIdAsync<TItem>(SqliteConnection connection, string id, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {Quote(GetCollectionNameForType<TItem>())} WHERE _id = @id);";
        command.Parameters.AddWithValue("@id", id);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) == 1;
    }

    private async Task<List<string>> SelectIdsAsync<TItem>(SqliteConnection connection, SqlFragment predicate, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT _id FROM {Quote(GetCollectionNameForType<TItem>())} WHERE {predicate.Sql};";

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private async Task<int> ExecuteSoftDeleteAsync<TItem>(SqliteConnection connection, SqlFragment predicate, DateTimeOffset now, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = json_set(_doc,
                        '$.IsDeleted', json('true'),
                        '$.DeletedAt', @now,
                        '$.DeletedBy', ''),
                _v = COALESCE(_v,0) + 1,
                _deleted = 1
            WHERE {predicate.Sql};
            """;

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        command.Parameters.AddWithValue("@now", now.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteRestoreAsync<TItem>(SqliteConnection connection, SqlFragment predicate, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {Quote(GetCollectionNameForType<TItem>())}
            SET _doc = json_set(_doc,
                        '$.IsDeleted', json('false'),
                        '$.DeletedAt', json('null'),
                        '$.DeletedBy', ''),
                _v = COALESCE(_v,0) + 1,
                _deleted = 0
            WHERE {predicate.Sql};
            """;

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteHardDeleteAsync<TItem>(SqliteConnection connection, SqlFragment predicate, CancellationToken cancellationToken)
        where TItem : Entity
    {
        await EnsureTableAsync<TItem>(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {Quote(GetCollectionNameForType<TItem>())} WHERE {predicate.Sql};";

        foreach (var parameter in predicate.Parameters)
        {
            command.Parameters.Add(CloneParameter(parameter));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureId<TItem>(TItem entity) where TItem : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityIdGenerator.GenerateNewId();
        }
    }

    private static Expression<Func<TItem, bool>> BuildIdFilter<TItem>(string id) where TItem : Entity
    {
        var normalized = NormalizeId(id);
        return item => item.Id == normalized;
    }

    private static Expression<Func<TItem, bool>> BuildIdsFilter<TItem>(IReadOnlyCollection<string> ids) where TItem : Entity
        => item => ids.Contains(item.Id);

    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Delete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Delete, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var fragment = filter is null ? SqlFragment.AlwaysTrue : new SqliteExpressionTranslator().Translate(filter);
            var ids = await SelectIdsAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);

            if (SupportsSoftDelete<TItem>())
            {
                await ExecuteSoftDeleteAsync<TItem>(lease.Connection, fragment, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExecuteHardDeleteAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);
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

    public Task Delete<TItem>(string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);
        return normalized is null ? Task.CompletedTask : Delete(BuildIdFilter<TItem>(normalized), transaction, cancellationToken);
    }

    public Task Delete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);
        return normalized.Count == 0 ? Task.CompletedTask : Delete(BuildIdsFilter<TItem>(normalized), transaction, cancellationToken);
    }

    public async Task Insert<TItem>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await ExecuteInsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, 1, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Insert<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Insert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Insert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var entity in list)
            {
                await ExecuteInsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Insert, context,
                BuildWriteResult(context, WriteOutcome.Inserted, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Save<TItem>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            var existed = await ExistsByIdAsync<TItem>(lease.Connection, entity.Id, cancellationToken).ConfigureAwait(false);
            await ExecuteUpsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Save<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Save, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Save, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var entity in list)
            {
                await ExecuteUpsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Save, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            var affected = await ExecuteUpdateByIdAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, affected, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var combined = conditionPredicate.And(item => item.Id == entity.Id);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, item: entity, filter: conditionPredicate, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            var fragment = new SqliteExpressionTranslator().Translate(combined);
            var affected = await ExecuteUpdateWhereAsync(lease.Connection, entity, fragment, cancellationToken).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new FailedToUpdateException();
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, affected, new[] { entity.Id })).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Update<TItem>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entities.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Update, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Update, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var entity in list)
            {
                var affected = await ExecuteUpdateByIdAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

                if (affected == 0)
                {
                    throw new FailedToUpdateException();
                }
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Update, context,
                BuildWriteResult(context, WriteOutcome.Updated, list.Count, list.Select(entity => entity.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Upsert<TItem>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        EnsureId(entity);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, item: entity, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            var existed = await ExistsByIdAsync<TItem>(lease.Connection, entity.Id, cancellationToken).ConfigureAwait(false);
            await ExecuteUpsertAsync(lease.Connection, entity, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, 1, new[] { entity.Id }, wasCreated: !existed)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task Upsert<TItem>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var list = entity.ToList();

        if (list.Count == 0)
        {
            return;
        }

        list.ForEach(EnsureId);

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Upsert, items: list, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Upsert, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

            foreach (var item in list)
            {
                await ExecuteUpsertAsync(lease.Connection, item, cancellationToken).ConfigureAwait(false);
            }

            await ApplyAfterBehaviors(RepositoryWriteOperation.Upsert, context,
                BuildWriteResult(context, WriteOutcome.Merged, list.Count, list.Select(item => item.Id))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task HardDelete<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => HardDeleteCore(filter, transaction, cancellationToken);

    public Task HardDelete<TItem>(string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);
        return normalized is null ? Task.CompletedTask : HardDeleteCore(BuildIdFilter<TItem>(normalized), transaction, cancellationToken);
    }

    public Task HardDelete<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);
        return normalized.Count == 0 ? Task.CompletedTask : HardDeleteCore(BuildIdsFilter<TItem>(normalized), transaction, cancellationToken);
    }

    private async Task HardDeleteCore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.HardDelete, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.HardDelete, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var fragment = filter is null ? SqlFragment.AlwaysTrue : new SqliteExpressionTranslator().Translate(filter);
            var ids = await SelectIdsAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);
            await ExecuteHardDeleteAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);

            await ApplyAfterBehaviors(RepositoryWriteOperation.HardDelete, context,
                BuildWriteResult(context, WriteOutcome.Deleted, ids.Count, ids)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
    }

    public Task Restore<TItem>(string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeId(id);
        return normalized is null ? Task.CompletedTask : RestoreCore(BuildIdFilter<TItem>(normalized), transaction, cancellationToken);
    }

    public Task Restore<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);
        return normalized.Count == 0 ? Task.CompletedTask : RestoreCore(BuildIdsFilter<TItem>(normalized), transaction, cancellationToken);
    }

    public Task Restore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => RestoreCore(filter, transaction, cancellationToken);

    private async Task RestoreCore<TItem>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity
    {
        if (!SupportsSoftDelete<TItem>())
        {
            throw new NotSupportedException($"Restore is not supported for non-soft-deletable entity '{typeof(TItem).Name}'.");
        }

        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Restore, filter: filter, transaction: transaction, cancellationToken: cancellationToken);
        await DispatchWriteBehaviorsAsync(RepositoryWriteOperation.Restore, context).ConfigureAwait(false);

        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
            await EnsureTableAsync<TItem>(lease.Connection, cancellationToken).ConfigureAwait(false);

            var fragment = filter is null ? SqlFragment.AlwaysTrue : new SqliteExpressionTranslator().Translate(filter);
            var ids = await SelectIdsAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);
            await ExecuteRestoreAsync<TItem>(lease.Connection, fragment, cancellationToken).ConfigureAwait(false);

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
        => throw new NotImplementedException("Transactions are implemented in Phase 5.");

    public Task JsonUpdate<TItem>(string id, int version, string json, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("JsonUpdate is implemented in Phase 4.");

    public Task Patch<TItem>(string id, long? expectedVersion = null, string jsonDocument = null!, IDataUpdateDefinition<TItem> updateDefinition = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Patch is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, int>> field, int delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, long>> field, long delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, double>> field, double delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task Increment<TItem>(string id, Expression<Func<TItem, decimal>> field, decimal delta, long? expectedVersion = null,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Increment is implemented in Phase 4.");

    public Task<CascadeReport> DeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Cascade is implemented in Phase 6.");

    public Task<CascadeReport> HardDeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Cascade is implemented in Phase 6.");
}
