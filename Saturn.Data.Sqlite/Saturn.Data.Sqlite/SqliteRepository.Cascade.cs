using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Cascade;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public async Task<CascadeReport> DeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var report = await RunCascadeAsync(typeof(TItem), id, forceMode: null, transaction, cancellationToken).ConfigureAwait(false);
        await Delete<TItem>(id, transaction, cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task<CascadeReport> HardDeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var report = await RunCascadeAsync(typeof(TItem), id, forceMode: CascadeMode.HardDelete, transaction, cancellationToken).ConfigureAwait(false);
        await HardDelete<TItem>(id, transaction, cancellationToken).ConfigureAwait(false);
        return report;
    }

    internal async Task<List<string>> MaterializeCascadeChildrenAsync(Type childType, string parentId, IDatabaseTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

        var collection = CollectionNameForType(childType);
        await EnsureTableAsync(collection, lease.Connection, cancellationToken).ConfigureAwait(false);

        var paths = CascadeRelationResolver.RefPathsForChild(childType);

        if (paths.Count == 0)
        {
            return new List<string>();
        }

        var predicates = paths.Select(path => $"{CascadeRelationResolver.PathExpression(path)} = @parent");
        var table = Quote(collection);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = $"SELECT _id FROM {table} WHERE {string.Join(" OR ", predicates)};";
        command.Parameters.AddWithValue("@parent", parentId);

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    internal async Task ApplyCascadeAsync(Type childType, IReadOnlyList<string> ids, CascadeMode mode, string parentId,
        IDatabaseTransaction? transaction, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

        var collection = CollectionNameForType(childType);
        await EnsureTableAsync(collection, lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(collection);
        var parameters = ids.Select((id, index) => new SqliteParameter($"@c{index}", id)).ToList();
        var names = string.Join(", ", parameters.Select(parameter => parameter.ParameterName));

        var sql = mode switch
        {
            CascadeMode.HardDelete => $"DELETE FROM {table} WHERE _id IN ({names});",
            CascadeMode.Archive =>
                $"""
                UPDATE {table}
                SET _doc = json_set(_doc,
                            '$.IsArchived', json('true'),
                            '$.ArchivedAt', @now,
                            '$.ArchivedBy', @parent,
                            '$.Version', COALESCE(_v,0) + 1),
                    _v = COALESCE(_v,0) + 1,
                    _archived = 1
                WHERE _id IN ({names});
                """,
            _ =>
                $"""
                UPDATE {table}
                SET _doc = json_set(_doc,
                            '$.IsDeleted', json('true'),
                            '$.DeletedAt', @now,
                            '$.DeletedBy', @parent,
                            '$.Version', COALESCE(_v,0) + 1),
                    _v = COALESCE(_v,0) + 1,
                    _deleted = 1
                WHERE _id IN ({names});
                """
        };

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        if (mode != CascadeMode.HardDelete)
        {
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@parent", parentId);
        }

        await command.ExecuteNonQueryWithRetryAsync(sqliteOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CascadeReport> RunCascadeAsync(Type parentType, string parentId, CascadeMode? forceMode,
        IDatabaseTransaction? transaction, CancellationToken cancellationToken)
    {
        var executor = new SqliteCascadeExecutor(this, forceMode);
        var deleted = new Dictionary<Type, int>();
        var archived = new Dictionary<Type, int>();
        var sharedDeletions = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedShared = new List<(Type, string, IReadOnlyList<string>)>();
        var skippedCycles = new List<(Type, string)>();
        var visited = new HashSet<(Type, string)>();
        var work = new Queue<(Type Type, string Id, CascadeDepth Depth)>();

        work.Enqueue((parentType, parentId, CascadeDepth.Transitive));

        while (work.Count > 0)
        {
            var (currentType, currentId, currentDepth) = work.Dequeue();

            if (!visited.Add((currentType, currentId)))
            {
                skippedCycles.Add((currentType, currentId));
                continue;
            }

            foreach (var relation in CascadeRelationResolver.RelationsForParent(currentType))
            {
                var effectiveMode = forceMode ?? relation.Mode;

                if (effectiveMode == CascadeMode.None)
                {
                    continue;
                }

                var step = new CascadeStep(relation.ChildType, currentId, Array.Empty<string>(), effectiveMode, relation.Depth, relation.SharedScope);
                var result = await executor.ExecuteAsync(step, transaction, cancellationToken).ConfigureAwait(false);

                if (effectiveMode == CascadeMode.Archive)
                {
                    archived[relation.ChildType] = archived.GetValueOrDefault(relation.ChildType) + result.AffectedIds.Count;
                }
                else
                {
                    deleted[relation.ChildType] = deleted.GetValueOrDefault(relation.ChildType) + result.AffectedIds.Count;
                }

                sharedDeletions.AddRange(result.SharedScopeDeletions.Select(item => (relation.ChildType, item.ChildId, item.OtherParentIds)));
                skippedShared.AddRange(result.SkippedSharedChildren.Select(item => (relation.ChildType, item.ChildId, item.OtherParentIds)));

                if (currentDepth == CascadeDepth.Transitive && relation.Depth == CascadeDepth.Transitive)
                {
                    foreach (var childId in result.AffectedIds)
                    {
                        work.Enqueue((relation.ChildType, childId, CascadeDepth.Transitive));
                    }
                }
            }
        }

        return new CascadeReport
        {
            DeletedPerType = deleted,
            ArchivedPerType = archived,
            SharedScopeDeletions = sharedDeletions,
            SkippedSharedChildren = skippedShared,
            SkippedCycles = skippedCycles,
            Warnings = Array.Empty<string>(),
            Aborted = false
        };
    }
}
