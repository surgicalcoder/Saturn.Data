# Phase 6 — Cascade, Change Feed and DI

**Goal:** Implement `DeleteCascade`/`HardDeleteCascade`, the outbox change-feed sink, and the DI extension.

**Prerequisite:** Phase 5 complete.

**Exit criteria:**
- `CascadeTests` green (all four shared cascade contract tests).
- `ChangeFeedAfterWriteTests` green, including transaction rollback of the feed row.
- `DocumentDbChangeFeedPollerTests` green.
- `ChangeFeedDiTests` green.

---

## Task 6.1 — Cascade: copy the resolution, replace the storage

Copy these from `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\` to `...\GoLive.Saturn.Data.DocumentDb\`:

| Source | Target | Change |
| --- | --- | --- |
| `Cascade\CascadeRelationResolver.cs` | `Cascade\CascadeRelationResolver.cs` | namespace only — this file is provider-agnostic |
| `Cascade\SqliteCascadeExecutor.cs` | `Cascade\DocumentDbCascadeExecutor.cs` | namespace, class name `DocumentDbCascadeExecutor`, and the two repository calls (below) |
| `SqliteRepository.Cascade.cs` | `DocumentDbRepository.Cascade.cs` | namespace, class name, and the two apply/materialise methods (below) |

`CascadeRelationResolver` is unchanged: it reflects `[CascadeDelete]`/`[CascadeDeleteOnScope]` and is not database-specific.

### Replace `MaterializeCascadeChildrenAsync` and `ApplyCascadeAsync`

`DocumentDbRepository.Cascade.cs` — replace the SQL bodies with these:

```csharp
    internal async Task<List<string>> MaterializeCascadeChildrenAsync(Type childType, string parentId, IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var method = typeof(DocumentDbRepository)
            .GetMethod(nameof(MaterializeCascadeChildrenCoreAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(childType);

        var result = (Task<List<string>>)method.Invoke(this, new object[] { childType, parentId, transaction, cancellationToken })!;

        return await result.ConfigureAwait(false);
    }

    private async Task<List<string>> MaterializeCascadeChildrenCoreAsync<TChild>(Type childType, string parentId, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TChild : Entity
    {
        var predicate = BuildScopePredicateForType<TChild>(parentId);
        var matches = await QueryRunner.ToListAsync(predicate, null, false, null, null, cancellationToken).ConfigureAwait(false);

        return matches.Select(entity => entity.Id).ToList();
    }

    private static Expression<Func<TChild, bool>> BuildScopePredicateForType<TChild>(string parentId) where TChild : Entity
    {
        var hasScopeId = typeof(TChild).GetProperty("ScopeId") is not null;

        if (!hasScopeId)
        {
            var idParameter = Expression.Parameter(typeof(TChild), "item");
            var idBody = Expression.Equal(Expression.Property(idParameter, nameof(Entity.Id)), Expression.Constant(parentId));

            return Expression.Lambda<Func<TChild, bool>>(idBody, idParameter);
        }

        var parameter = Expression.Parameter(typeof(TChild), "item");
        var property = Expression.Property(parameter, "ScopeId");
        var body = Expression.Equal(property, Expression.Constant(parentId));

        return Expression.Lambda<Func<TChild, bool>>(body, parameter);
    }

    internal async Task ApplyCascadeAsync(Type childType, IReadOnlyList<string> ids, CascadeMode mode, string parentId,
        IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var method = typeof(DocumentDbRepository)
            .GetMethod(nameof(ApplyCascadeCoreAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(childType);

        var result = (Task)method.Invoke(this, new object[] { childType, ids, mode, parentId, transaction, cancellationToken })!;

        await result.ConfigureAwait(false);
    }

    private async Task ApplyCascadeCoreAsync<TChild>(Type childType, IReadOnlyList<string> ids, CascadeMode mode, string parentId,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TChild : Entity
    {
        if (mode == CascadeMode.HardDelete)
        {
            await RemoveWithTransactionAsync<TChild>(transaction, ids, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var id in ids)
        {
            var entity = await Store.Get<TChild>(id).ConfigureAwait(false);

            if (entity is null)
            {
                continue;
            }

            if (mode == CascadeMode.Archive && entity is IArchivable archivable)
            {
                archivable.IsArchived = true;
                archivable.ArchivedAt = DateTimeOffset.UtcNow;
                archivable.ArchivedBy = parentId;
            }
            else if (entity is ISoftDeletable deletable)
            {
                deletable.IsDeleted = true;
                deletable.DeletedAt = DateTimeOffset.UtcNow;
                deletable.DeletedBy = parentId;
            }

            entity.Version = (entity.Version ?? 0) + 1;
            await UpdateWithTransactionAsync(transaction, entity, cancellationToken).ConfigureAwait(false);
        }
    }
```

Keep `RunCascadeAsync` from the SQLite provider unchanged, and keep the public `DeleteCascade`/`HardDeleteCascade` methods (they run the traversal then delete the root entity).

> **Membership limitation:** children reached only through `MultiscopedEntity.Scopes` (i.e. no `ScopeId`) are matched on `Id` here. Full `Scopes` membership requires an array-unnest predicate that MariaDB does not support; if a cascade test needs it, gate it with `Capabilities.SupportsCollectionPredicates` and record the limitation in the README. The shared cascade entities used by the contract tests are scope-based, so `ScopeId` covers them.

---

## Task 6.2 — Change feed: internal row types

`...\GoLive.Saturn.Data.DocumentDb\ChangeFeed\ChangeFeedRows.cs`:

```csharp
namespace Saturn.Data.DocumentDb.ChangeFeed;

internal sealed class ChangeFeedCounterRow
{
    public string Id { get; set; } = "";

    public long Seq { get; set; }
}

internal sealed class ChangeFeedOutboxRow
{
    public string Id { get; set; } = "";

    public long Seq { get; set; }

    public string Source { get; set; } = "";

    public string PayloadJson { get; set; } = "";
}
```

> The outbox stores each `ChangeFeedRecord` as a JSON payload rather than one column per field. This avoids per-type column mapping and works identically on every backend. The SQLite provider used discrete columns; this is a deliberate divergence because Shiny.DocumentDb stores documents, not rows.

---

## Task 6.3 — Change feed sink

`...\GoLive.Saturn.Data.DocumentDb\ChangeFeed\DocumentDbOutboxChangeFeedSink.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;

namespace Saturn.Data.DocumentDb.ChangeFeed;

public sealed class DocumentDbOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private const int MaxAllocationAttempts = 16;

    private readonly DocumentDbRepository repository;

    public DocumentDbOutboxChangeFeedSink(DocumentDbRepository repository, string source) : base(source)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAllocationAttempts; attempt++)
        {
            var counterId = $"counter:{Source}";
            var counter = await repository.Store.Get<ChangeFeedCounterRow>(counterId).ConfigureAwait(false);
            var next = (counter?.Seq ?? 0) + 1;

            if (counter is null)
            {
                await repository.Store.Insert(new ChangeFeedCounterRow { Id = counterId, Seq = next }).ConfigureAwait(false);
            }
            else
            {
                counter.Seq = next;
                await repository.Store.Update(counter).ConfigureAwait(false);
            }

            return next;
        }

        throw new InvalidOperationException($"Could not allocate a change feed sequence for source '{Source}'.");
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);

        var row = new ChangeFeedOutboxRow
        {
            Id = $"{record.Sequence.ToString("D20", CultureInfo.InvariantCulture)}",
            Seq = record.Sequence,
            Source = record.Source,
            PayloadJson = JsonSerializer.Serialize(record)
        };

        try
        {
            await repository.Store.Insert(row).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        var rows = await repository.Store.Query<ChangeFeedOutboxRow>()
            .Where(row => row.Source == source && row.Seq > afterSequence)
            .OrderBy(row => row.Seq)
            .Paginate(0, take)
            .ToList()
            .ConfigureAwait(false);

        var result = new List<DataChangeEvent>();

        foreach (var row in rows)
        {
            var record = JsonSerializer.Deserialize<ChangeFeedRecord>(row.PayloadJson);

            if (record is not null)
            {
                result.Add(ChangeFeedRecordMapper.ToEvent(record));
            }
        }

        return result;
    }
}
```

**Transaction participation.** The base class passes the caller's `transaction` into `NextSequenceAsync`/`AppendRowAsync`. Route the counter and row writes through the Phase 5 helpers so a rolled-back transaction discards them:

- In `NextSequenceAsync`, replace `repository.Store.Insert(...)` / `repository.Store.Update(...)` with the repository's transaction-aware helpers. Because those helpers are `protected`, add internal wrappers on `DocumentDbRepository`:

```csharp
    internal Task InsertCounterAsync(IDatabaseTransaction transaction, ChangeFeedCounterRow row, CancellationToken cancellationToken)
        => InsertWithTransactionAsync(transaction, row, cancellationToken);

    internal Task UpdateCounterAsync(IDatabaseTransaction transaction, ChangeFeedCounterRow row, CancellationToken cancellationToken)
        => UpdateWithTransactionAsync(transaction, row, cancellationToken);

    internal Task InsertOutboxRowAsync(IDatabaseTransaction transaction, ChangeFeedOutboxRow row, CancellationToken cancellationToken)
        => InsertWithTransactionAsync(transaction, row, cancellationToken);
```

Then use those in the sink. `ReadRowsAsync` always reads outside the transaction.

**Sequencing semantics to document:** the counter is incremented before the row is written, so a concurrent writer can leave a **gap** in the sequence, and a lost increment keeps the trailing row's Id unique (the padded `Id` acts as the conflict detector). Gaps are harmless; the poller only requires strictly increasing order. If `MapVersionProperty` is ever registered for the counter type, the retry loop can be tightened — but that requires per-type config, which this provider avoids.

---

## Task 6.4 — DI extension

`...\GoLive.Saturn.Data.DocumentDb\ChangeFeed\ChangeFeedServicesExtensions.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Data.DocumentDb.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddDocumentDbChangeFeed(this IServiceCollection services, DocumentDbRepository repository, string source)
    {
        var sink = new DocumentDbOutboxChangeFeedSink(repository, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
```

Also add the repository DI extension mirroring the SQLite provider:

`...\ServicesExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace Saturn.Data.DocumentDb;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",
        AsImplementedInterfaces = true,
        AsSelf = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnDocumentDbRepositoryServices(this IServiceCollection services);
}
```

---

## Task 6.5 — Tests

Port from `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\`, changing namespace and type names:

| Source | Target | Notes |
| --- | --- | --- |
| `CascadeTestFixture.cs` | `CascadeTestFixture.cs` | replace the option object with `DocumentDbRepositoryOptions` + `SqliteDatabaseProvider` |
| `CascadeTests.cs` | `CascadeTests.cs` | `CascadeContractTests<CascadeTestFixture, UnitTestableDocumentDbRepository>` |
| `ChangeFeedTestFixture.cs` | `ChangeFeedTestFixture.cs` | options as above; sink type `DocumentDbOutboxChangeFeedSink`; expose `SupportsTransactions => Repository.Capabilities.SupportsTransactions` |
| `ChangeFeedAfterWriteTests.cs` | `ChangeFeedAfterWriteTests.cs` | `ChangeFeedContractTests<ChangeFeedTestFixture, UnitTestableDocumentDbRepository>` |
| `SqliteChangeFeedPollerTests.cs` | `DocumentDbChangeFeedPollerTests.cs` | `ChangeFeedPollerTests<...>` |
| `ChangeFeedDiTests.cs` | `ChangeFeedDiTests.cs` | assert `IsType<DocumentDbOutboxChangeFeedSink>` |

The change-feed fixture's `DropRecreateDatabase` equivalent is just a fresh temp database path per fixture instance; there is no shared connection pool to clear.

---

## Task 6.6 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

---

## Do NOT

- Do not use `IChangeFeedDocumentStore.SubscribeChanges` — decision 4 is outbox-with-CAS.
- Do not add per-type configuration for the counter row.
- Do not write the outbox outside the caller's transaction.
- Do not use the library's `AddOutbox` feature.
- Do not add comments to `.cs` files.
