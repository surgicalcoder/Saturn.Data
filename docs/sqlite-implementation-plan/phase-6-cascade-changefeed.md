# Phase 6 — Cascade, Change Feed, DI

**Goal:** Implement `DeleteCascade` / `HardDeleteCascade`, the outbox change-feed sink, and the DI helpers.

**Prerequisite:** Phase 5 complete and all tests green.

**Exit criteria:**
- `CascadeTests` pass (all 4 shared cascade contract tests).
- `ChangeFeedAfterWriteTests` pass (all shared change-feed contract tests including transaction rollback of the feed row).
- `SqliteChangeFeedPollerTests` pass.
- `ChangeFeedDiTests` pass.

---

## Task 6.1 — Add internal helpers to `SqliteRepository.cs`

Add these members:

```csharp
    internal string CollectionNameForType(Type type) => GetCollectionNameForType(type);

    internal async Task EnsureTableForTypeAsync(Type type, SqliteConnection connection, CancellationToken cancellationToken)
        => await EnsureTableAsync(CollectionNameForType(type), connection, cancellationToken).ConfigureAwait(false);

    internal static Type CascadeElementType(Type childType)
    {
        if (childType.GetProperty("ScopeId") is not null)
        {
            return childType;
        }

        return childType;
    }
```

> If a `CollectionNameForType(Type)` overload already exists, do not duplicate it.

---

## Task 6.2 — Create `Cascade\SqliteCascadeExecutor.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Cascade\SqliteCascadeExecutor.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Cascade;

public sealed class SqliteCascadeExecutor : ICascadeExecutor
{
    private readonly SqliteRepository repository;
    private readonly CascadeMode? forceMode;

    public SqliteCascadeExecutor(SqliteRepository repository, CascadeMode? forceMode = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.forceMode = forceMode;
    }

    public bool Supports(Type childType) => typeof(Entity).IsAssignableFrom(childType);

    public async Task<CascadeStepResult> ExecuteAsync(CascadeStep step, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var mode = forceMode ?? step.Mode;
        var ids = step.ChildIds is { Count: > 0 }
            ? step.ChildIds.ToList()
            : await repository.MaterializeCascadeChildrenAsync(step.ChildType, step.ParentId, transaction, cancellationToken).ConfigureAwait(false);

        if (ids.Count == 0)
        {
            return new CascadeStepResult(Array.Empty<string>(), Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
        }

        await repository.ApplyCascadeAsync(step.ChildType, ids, mode, step.ParentId, transaction, cancellationToken).ConfigureAwait(false);

        return new CascadeStepResult(ids, Array.Empty<(string, IReadOnlyList<string>)>(), Array.Empty<(string, IReadOnlyList<string>)>());
    }
}
```

---

## Task 6.3 — Create `SqliteRepository.Cascade.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRepository.Cascade.cs`

```csharp
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.Cascade;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;
using Microsoft.Data.Sqlite;
using Saturn.Data.Sqlite.Cascade;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    public Task<CascadeReport> DeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => RunCascadeAsync(typeof(TItem), id, forceMode: null, transaction, cancellationToken);

    public Task<CascadeReport> HardDeleteCascade<TItem>(string id, CascadeMode mode = CascadeMode.Default, CascadeDepth depth = CascadeDepth.Single,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
        => RunCascadeAsync(typeof(TItem), id, forceMode: CascadeMode.HardDelete, transaction, cancellationToken);

    internal async Task<List<string>> MaterializeCascadeChildrenAsync(Type childType, string parentId, IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableForTypeAsync(childType, lease.Connection, cancellationToken).ConfigureAwait(false);

        var hasScopeId = childType.GetProperty("ScopeId") is not null;
        var hasScopes = childType.GetProperty("Scopes") is not null;
        var table = Quote(CollectionNameForType(childType));

        var sql = hasScopeId
            ? $"SELECT _id FROM {table} WHERE _scope = @parent;"
            : hasScopes
                ? $"SELECT _id FROM {table} WHERE EXISTS (SELECT 1 FROM json_each(_doc,'$.Scopes') WHERE value = @parent);"
                : $"SELECT _id FROM {table} WHERE _id = @parent;";

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
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
        IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var lease = await RentConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        await EnsureTableForTypeAsync(childType, lease.Connection, cancellationToken).ConfigureAwait(false);

        var table = Quote(CollectionNameForType(childType));
        var parameters = ids.Select((id, index) => new SqliteParameter($"@c{index}", id)).ToList();
        var names = string.Join(", ", parameters.Select(parameter => parameter.ParameterName));

        var sql = mode switch
        {
            CascadeMode.HardDelete => $"DELETE FROM {table} WHERE _id IN ({names});",
            CascadeMode.Archive =>
                $"""
                UPDATE {table}
                SET _doc = json_set(_doc, '$.IsArchived', json('true'), '$.ArchivedAt', @now, '$.ArchivedBy', @parent),
                    _v = COALESCE(_v,0) + 1,
                    _archived = 1
                WHERE _id IN ({names});
                """,
            _ =>
                $"""
                UPDATE {table}
                SET _doc = json_set(_doc, '$.IsDeleted', json('true'), '$.DeletedAt', @now, '$.DeletedBy', @parent),
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
        IDatabaseTransaction transaction, CancellationToken cancellationToken)
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

            foreach (var relation in GetCascadeSnapshot(currentType))
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

    private static IReadOnlyList<CascadeRelationSnapshot> GetCascadeSnapshot(Type parentType)
    {
        var property = parentType.GetProperty("__Cascade", BindingFlags.Public | BindingFlags.Static);

        if (property is null)
        {
            return Array.Empty<CascadeRelationSnapshot>();
        }

        var snapshotProperty = property.PropertyType.GetProperty("For", BindingFlags.Public | BindingFlags.Static);

        if (snapshotProperty is null)
        {
            return Array.Empty<CascadeRelationSnapshot>();
        }

        var value = snapshotProperty.GetValue(null);
        return value as IReadOnlyList<CascadeRelationSnapshot> ?? Array.Empty<CascadeRelationSnapshot>();
    }
}
```

---

## Task 6.4 — Create `ChangeFeed\SqliteOutboxChangeFeedSink.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\ChangeFeed\SqliteOutboxChangeFeedSink.cs`

```csharp
using System.Globalization;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.ChangeFeed;

public sealed class SqliteOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private readonly SqliteRepository repository;

    public SqliteOutboxChangeFeedSink(SqliteRepository repository, string source) : base(source)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        await repository.InitializeAsync(ct).ConfigureAwait(false);
        await using var lease = await repository.RentConnectionInternalAsync(transaction, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "__change_feed_counters"(source, entity_type, seq)
            VALUES (@source, @type, 1)
            ON CONFLICT(source, entity_type) DO UPDATE SET seq = seq + 1
            RETURNING seq;
            """;
        command.Parameters.AddWithValue("@source", Source);
        command.Parameters.AddWithValue("@type", change.EntityType.FullName);

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);

        await repository.InitializeAsync(ct).ConfigureAwait(false);
        await using var lease = await repository.RentConnectionInternalAsync(transaction, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "__change_feed"
                (seq, change_id, source, entity_type, occurred_utc, operation, outcome, entity_ids, is_partial, has_full_items, items_json, version)
            VALUES
                (@seq, @changeId, @source, @entityType, @occurredUtc, @operation, @outcome, @entityIds, @isPartial, @hasFullItems, @itemsJson, @version);
            """;
        command.Parameters.AddWithValue("@seq", record.Sequence);
        command.Parameters.AddWithValue("@changeId", record.Id);
        command.Parameters.AddWithValue("@source", record.Source);
        command.Parameters.AddWithValue("@entityType", record.EntityTypeName);
        command.Parameters.AddWithValue("@occurredUtc", record.OccuredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@operation", record.Operation);
        command.Parameters.AddWithValue("@outcome", record.Outcome);
        command.Parameters.AddWithValue("@entityIds", JsonSerializer.Serialize(record.EntityIds));
        command.Parameters.AddWithValue("@isPartial", record.IsPartial ? 1 : 0);
        command.Parameters.AddWithValue("@hasFullItems", record.HasFullItems ? 1 : 0);
        command.Parameters.AddWithValue("@itemsJson", (object)record.ItemsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@version", (object)record.Version ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        await repository.InitializeAsync(ct).ConfigureAwait(false);
        await using var lease = await repository.RentConnectionInternalAsync(null, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT seq, change_id, source, entity_type, occurred_utc, operation, outcome, entity_ids, is_partial, has_full_items, items_json, version
            FROM "__change_feed"
            WHERE source = @source AND seq > @after
            ORDER BY seq
            LIMIT @take;
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@after", afterSequence);
        command.Parameters.AddWithValue("@take", take);

        var result = new List<DataChangeEvent>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var record = new ChangeFeedRecord
            {
                Sequence = reader.GetInt64(0),
                Id = reader.GetString(1),
                Source = reader.GetString(2),
                EntityTypeName = reader.GetString(3),
                OccuredAtUtc = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                Operation = reader.GetString(5),
                Outcome = reader.GetString(6),
                EntityIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(7)) ?? new List<string>(),
                IsPartial = reader.GetInt32(8) == 1,
                HasFullItems = reader.GetInt32(9) == 1,
                ItemsJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                Version = reader.IsDBNull(11) ? null : reader.GetInt64(11)
            };

            result.Add(ChangeFeedRecordMapper.ToEvent(record));
        }

        return result;
    }

    private static async Task EnsureTablesAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__change_feed" (
                seq INTEGER NOT NULL PRIMARY KEY,
                change_id TEXT NOT NULL,
                source TEXT NOT NULL,
                entity_type TEXT NOT NULL,
                occurred_utc TEXT NOT NULL,
                operation TEXT NOT NULL,
                outcome TEXT NOT NULL,
                entity_ids TEXT NOT NULL,
                is_partial INTEGER NOT NULL,
                has_full_items INTEGER NOT NULL,
                items_json TEXT NULL,
                version INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS "ix___change_feed_source_seq" ON "__change_feed"(source, seq);
            CREATE TABLE IF NOT EXISTS "__change_feed_counters" (
                source TEXT NOT NULL,
                entity_type TEXT NOT NULL,
                seq INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (source, entity_type)
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
```

---

## Task 6.5 — Create `ChangeFeed\ChangeFeedServicesExtensions.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\ChangeFeed\ChangeFeedServicesExtensions.cs`

```csharp
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Data.Sqlite.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddSqliteChangeFeed(this IServiceCollection services, SqliteRepository repository, string source)
    {
        var sink = new SqliteOutboxChangeFeedSink(repository, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
```

---

## Task 6.6 — Replace the Phase 1 cascade stubs

In `SqliteRepository.Repository.cs`, remove the `DeleteCascade` and `HardDeleteCascade` stub methods that throw `NotSupportedException`. The implementations now live in `SqliteRepository.Cascade.cs`. If the stubs remain, the Cascade casts will throw.

---

## Task 6.7 — Tests

### 6.7.1 `CascadeTestFixture.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared.Cascade;

namespace Saturn.Data.Sqlite.Tests;

public class CascadeTestFixture : IDisposable, ICascadeTestFixture<UnitTestableSqliteRepository>
{
    public UnitTestableSqliteRepository Repository { get; }

    public CascadeTestFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-cascade-{Guid.NewGuid():N}.db");
        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new SqliteRepositoryOptions { DataSource = path });
        Repository.DropRecreateDatabase();
    }

    public void Dispose() => Repository.Dispose();
}
```

### 6.7.2 `CascadeTests.cs`

```csharp
using Saturn.Data.Testing.Shared.Cascade;

namespace Saturn.Data.Sqlite.Tests;

public class CascadeTests(CascadeTestFixture fixture)
    : CascadeContractTests<CascadeTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<CascadeTestFixture>;
```

### 6.7.3 `ChangeFeedTestFixture.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.Sqlite.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableSqliteRepository>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => true;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; private set; }

    public UnitTestableSqliteRepository Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();

        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-changefeed-{Guid.NewGuid():N}.db");
        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name,
                WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
            },
            new SqliteRepositoryOptions { DataSource = path });
        Repository.DropRecreateDatabase();

        Sink = new SqliteOutboxChangeFeedSink(Repository, "test-source");
        Repository.Options.WriteBehaviors.Add(new ChangeFeedBehavior(Sink, "test-source", new ChangeFeedBehaviorOptions { Enabled = true }));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose() => Repository.Dispose();
}
```

### 6.7.4 `ChangeFeedAfterWriteTests.cs`

```csharp
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedAfterWriteTests(ChangeFeedTestFixture fixture)
    : ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
```

### 6.7.5 `SqliteChangeFeedPollerTests.cs`

```csharp
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class SqliteChangeFeedPollerTests(ChangeFeedTestFixture fixture)
    : ChangeFeedPollerTests<ChangeFeedTestFixture, UnitTestableSqliteRepository>(fixture), IClassFixture<ChangeFeedTestFixture>;
```

### 6.7.6 `ChangeFeedDiTests.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;
using Saturn.Data.Sqlite.ChangeFeed;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedDiTests(ChangeFeedTestFixture fixture)
{
    [Fact]
    public async Task AddSqliteChangeFeed_Registers_Sink_And_Poller()
    {
        var services = new ServiceCollection();
        services.AddSqliteChangeFeed(fixture.Repository, "test-source");
        using var provider = services.BuildServiceProvider();

        var sink = provider.GetRequiredService<IChangeFeedSink>();
        var poller = provider.GetRequiredService<ChangeFeedPoller>();

        Assert.IsType<SqliteOutboxChangeFeedSink>(sink);
        Assert.NotNull(poller);

        var entityId = Guid.NewGuid().ToString("N");
        var delivered = new List<DataChangeEvent<ChangeFeedEntity>>();
        await poller.For<ChangeFeedEntity>("test-source").SubscribeAsync((change, ct) => { delivered.Add(change); return ValueTask.CompletedTask; });

        await sink.AppendAsync(new DataChangeEvent
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            Source = "test-source",
            OccuredAtUtc = DateTime.UtcNow,
            EntityType = typeof(ChangeFeedEntity),
            Operation = RepositoryWriteOperation.Insert,
            Outcome = WriteOutcome.Inserted,
            EntityIds = new[] { entityId },
            Version = 1
        }, null, CancellationToken.None);

        await poller.DrainAsync();

        Assert.Contains(delivered, change => change.EntityIds.Contains(entityId));
    }
}
```

---

## Task 6.8 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

---

## Troubleshooting

- **Cascade tests find no relations:** the `__Cascade` snapshot is generated by the `Saturn.Generator.Cascade` analyzer referenced by `Saturn.Data.Testing.Shared`. Confirm the shared project builds and the cascade entities are `partial`. If `GetCascadeSnapshot` returns empty, the generator did not run; do not hand-roll relations.
- **`Tx_Abort_Removes_Feed_Row` fails:** the sink must use the transaction connection. Verify `RentConnectionInternalAsync` returns the wrapper connection when `transaction is SqliteTransactionWrapper`.
- **`Bulk_Insert_Fires_Once` fails:** the batch `Insert` must dispatch behaviors exactly once with all ids.

---

## Do NOT

- Do not modify `CascadeEngine`; the SQLite cascade uses its own traversal so `HardDeleteCascade` can force the mode.
- Do not add cascade handling to read paths.
- Do not use a different change-feed table names; the sink owns `__change_feed` and `__change_feed_counters`.
- Do not add comments to `.cs` files.
