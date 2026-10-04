# Porting LiteDbX.Migrations to Saturn.Data — Proposal

**Status:** Draft for review (design proposal, not approved)
**Author:** Axiom
**Date:** 2026-10-04
**Scope:** Re-home the `LiteDbX.Migrations` raw-document migration engine onto Saturn.Data and make it run across all providers (`LiteDbX`, `MongoDb`, `Sqlite`, `DocumentDb`, `Stellar`).

Reference implementation analyzed:
- Engine: `D:\Work\LiteDBX\LiteDbX.Migrations\` (~4,400 LoC, 15 source files)
- Real usage: `D:\Work\GoLive.Misc\GoLive.LunarFlare\GoLive.LunarFlare.LiteDbxMigrator\Program.cs`
- Docs: `D:\Work\LiteDBX\docs\migration.md`, `D:\Work\LiteDBX\docs\migration-cleanup\`

---

## 1. Executive summary

`LiteDbX.Migrations` is a raw-BSON, provider-internal migration library. It scans physical collections as `LiteDbX.BsonDocument`, mutates fields by path (`Profile.CustomerId`, `Scopes[*]`, `**.LegacyId`), repairs references after `_id` rebuilds, and journals applied migrations. It is excellent at one job and hard-wired to one storage engine.

Saturn.Data has **no migration subsystem at all** (grep for `Migration` across the repo returns nothing). It also has no raw-document API: `IRepository` is strictly entity-typed, and the five providers store documents in five fundamentally different shapes.

This proposal ports the engine into a new provider-agnostic core (`GoLive.Saturn.Data.Migrations`) plus a thin storage adapter per provider. The engine keeps the exact fluent DSL, path grammar, predicate catalog, operations, reporting, dry-run, backups and id-remap semantics. The storage layer is abstracted behind a small `IMigrationStore` / `IMigrationCollection` contract so the same migration definition runs everywhere.

The honest constraint: **providers are not equally capable.** `LiteDbX`, `MongoDb` and `Sqlite` can support the full rebuild/swap workflow; `DocumentDb` (single shared table discriminated by `TypeName`) supports it with a re-tag rebuild; `Stellar` (MessagePack blobs, no JSON, no transactions, no indexes, no rename) can support at best a limited in-place, typed-map migration. The proposal therefore defines a **capability matrix and three tiers** rather than pretending parity.

Decision: **one engine over an abstract mutable document tree, per-provider adapters, tiered capabilities** (Option C, §4). Do not fork the engine per provider, and do not try to canonicalize everything through MongoDB.Bson. Two provider correctness fixes are prerequisites to the port and are folded into Phase 0: the soft-delete filter must be "deleted iff `IsDeleted == true`", and LiteDbX must persist `Properties` (`_p`) when non-empty (see §3.6).

---

## 2. What LiteDbX.Migrations actually is

### 2.1 Component inventory

| File | Role | Provider coupling |
| --- | --- | --- |
| `BsonPathNavigator.cs` | Path parser + resolver + mutate/remove/add; wildcard `[*]`, recursive `**`, prune-empty-parents, type-safe clone, path-conflict analysis | `BsonDocument`, `BsonValue`, `BsonArray`, `ObjectId`, `BsonType` |
| `CollectionMigrationBuilder.cs` | Fluent DSL + all operation implementations (`RemoveFieldWhen`, `AddFieldWhen`, `SetFieldWhen`, `ModifyFieldWhen`, `ModifyDocumentWhen`, transfers, `ConvertField`, `ConvertId`, `RepairReference`, `InsertDocumentWhen`, `RemoveWhere`, `PruneEmptyContainers`) | `BsonDocument`/`BsonValue` + engine-internal contexts |
| `MigrationRunner.cs` | Orchestration: selector resolution, in-place pass, rebuild/swap pass, index replay, backup retention, id-remap writes, progress, journal, backup cleanup | `ILiteDatabase`, `ILiteCollection<BsonDocument>`, `BsonExpression` |
| `MigrationDefinition.cs` | Plan/report DTOs (`MigrationReport`, `MigrationExecutionResult`, `CollectionMigrationResult`, `RebuildValidationSummary`, `MigrationBuilder`) | none (pure) |
| `MigrationCollectionSelector.cs` | Exact + glob collection selection, system-collection exclusion, backup/shadow exclusion | `ILiteDatabase` enumeration |
| `MigrationHistoryStore.cs` | Applied-migration journal (`__migrations`) | `ILiteCollection<BsonDocument>` |
| `IdRemapLog.cs` | Old→new id mapping log (`__migration_id_mappings`) | `ILiteCollection<BsonDocument>`, `ObjectId` |
| `MigrationRunOptions.cs` | Runner defaults + per-run options, backup retention, cleanup report DTOs | none (pure) |
| `MigrationProgress.cs` | Progress event DTO + stages | none (pure) |
| `BsonPredicateContext.cs` / `BsonPredicates.cs` | Predicate context + catalog (`EmptyArray`, `EmptyDocument`, `NullOrMissing`, `StructurallyEmpty`, `UselessValueAggressive`, composition) | `BsonDocument`/`BsonValue`/`BsonType` |
| `DocumentMigrationExecutionContext.cs` | Strict-path failures, invalid-value sampling, remap lookup, counters | `BsonDocument`/`BsonValue`/`ObjectId` |
| `CollectionInsertOperations.cs` | Collection-level ops (seed inserts) and their execution context | `ILiteCollection<BsonDocument>` |
| `InvalidObjectIdPolicy.cs` | `Fail \| SkipDocument \| LeaveUnchanged \| RemoveField \| GenerateNewId` | none |
| `LiteDatabaseMigrationExtensions.cs` | `db.Migrations()` entry point | `ILiteDatabase` |

### 2.2 Behavior that must survive the port

The engine is not just a renamer of fields. Ten behaviors are load-bearing and must be preserved exactly:

1. **Path grammar** — top-level, dotted nested, fixed indices `[0]`, wildcards `[*]`, recursive `**`, with `StrictPathResolution` toggling whether mixed document shapes are a no-op or a failure.
2. **`_id` rebuild semantics** — `ConvertId()` forces a shadow-copy + rename/swap rebuild, with `Fail | SkipDocument | GenerateNewId` policies (the other two policies are rejected for `_id`).
3. **Id remap + reference repair** — generated ids are logged; `RepairReference("Customer.$id").FromCollection("customers").FromMigration(...).WhenReferenceCollectionIs("Customer.$ref").Apply()` rewrites dangling references using the log.
4. **Backup + shadow lifecycle** — `{collection}__backup__{runId8}` and `{collection}__migrating__{runId8}`; retention `KeepAll | DeleteOnSuccess`; `CleanupBackupsAsync` with `KeepLatestCount`.
5. **Journaling** — migration keyed by name in a journal collection; dry-run does not mark applied; `WasSkipped` is derived.
6. **Dry-run** — full scan and report, zero writes, planned backup disposition.
7. **Reporting** — per-migration, per-selector, per-collection counts, invalid-value samples, duplicate-target-id samples, rebuild validation, generated mappings, repaired references.
8. **Progress** — `MigrationStarted | CollectionStarted | CollectionCompleted | MigrationCompleted` with cumulative counters.
9. **Predicate catalog + composition** — including the aggressive "useless value" recursive cleanup (`RemoveWhere(UselessValueAggressive, Recursive)` + `PruneEmptyContainers`).
10. **Collection selection** — exact, `*`, `tenant_*`, with automatic exclusion of `$*`, backups, shadows, journal and id-map.

### 2.3 The one real-world migration (LunarFlare)

`Program.cs` is the canonical porting target:

- Open `LiteDatabase.Open(path)`.
- Migration `20260410-IDMigration`: on all collections, `ConvertId().FromStringToObjectId().OnInvalidString(GenerateNewId)` and `RemoveFieldWhen("Properties", EmptyDocument)`.
- Migration `20240411-OtherFieldsMigrations`: convert `SecondScope`, `Scope`, `Scopes[*]`, `Permissions[*]`, `Groups[*]`, `ParentItemId` string→ObjectId (`Fail`), and remove `Payload` when null.
- `WithBackupRetention(DeleteOnSuccess).RunAsync()`.
- `db.Rebuild()` (engine compaction).
- Then open a `LiteDbRepository` and seed.

This is legacy repair: pre-Saturn data wrote `_id`/refs as strings and must be rewritten to ObjectId. The Saturn port must express exactly this, per provider, with provider-appropriate storage semantics.

---

## 3. Saturn constraints

### 3.1 No raw-document API exists

`IRepository` / `IReadonlyRepository` are entity-typed. Providers hold their handles behind `protected`/`internal` members:

- `Saturn.Data.LiteDbX`: `protected LiteDatabase database` (`LiteDbRepository.cs:13`), `internal ILiteTransaction ResolveLiteTransaction(...)`.
- `Saturn.Data.MongoDb`: `protected IMongoClient client`, `protected IMongoDatabase mongoDatabase` (`MongoDbRepository.cs:679-680`).
- `Saturn.Data.Sqlite`: `internal SqliteConnectionFactory ConnectionFactory`, `internal Task<SqliteConnectionLease> RentConnectionAsync`, internal `knownTables` cache.
- `Saturn.Data.DocumentDb`: `internal IDocumentStore store` (`DocumentDbRepository.cs:13`), only public if the caller supplied `DocumentDbRepositoryOptions.Store`.
- `Saturn.Data.Stellar`: `protected FastDB database` (`StellarRepository.cs:15`).

Consequence: **the migration store adapter must be implemented inside each provider assembly** (or the provider must expose a minimal public capability interface). A separate adapter assembly cannot reach these handles without `InternalsVisibleTo` that would have to be added anyway. Keeping adapters in-provider also lets them reuse `RepositoryOptions.GetCollectionName`, `EntityIdGenerator`, and provider serializers, which is essential for byte-compatible writes.

### 3.2 Per-provider storage reality

| Dimension | LiteDbX | MongoDb | Sqlite | DocumentDb | Stellar |
| --- | --- | --- | --- | --- | --- |
| Backend | embedded BSON file | MongoDB server | SQLite JSON1 | Shiny.DocumentDb (DuckDB/SQLite) | FastDB |
| Raw tree type | `LiteDbX.BsonDocument` | `MongoDB.Bson.BsonDocument` | `System.Text.Json.Nodes.JsonObject` | STJ `JsonObject` | MessagePack map |
| Physical unit | one collection/type | one collection/type | one table/type | **one shared table** (`documents`) | one file/type |
| Payload | BSON doc | BSON doc | `_doc TEXT` JSON | `Data` JSON | MessagePack |
| Logical collection key | collection name | collection name | table name | `TypeName` discriminator | collection file name |
| Entity `Id` on disk | `_id` ObjectId | `_id` ObjectId (`_v`, `_p`) | `_id` column + `Id` JSON string | `Id` column + `Id` in JSON | `EntityId` key + `Id` in map |
| `Ref<T>` on disk | ObjectId scalar | ObjectId scalar | JSON string | JSON string | MessagePack string |
| Soft delete | `IsDeleted == false` **(to fix → deleted iff `true`)** | `exists(IsDeleted=false) OR false` (already correct) | `_deleted=0` + `$.IsDeleted` | `IsDeleted` in body | `IsDeleted` in map |
| Rows visible when deleted | via `includeDeleted` | via `includeDeleted` | `_deleted` column | post-filter | post-filter |
| Index enumeration | none public | driver `Indexes.ListAsync` | only `PRAGMA index_list` (test-only today) | Shiny SQL builders | none |
| Rename/drop collection | engine API exists, not surfaced | driver supports both | `ALTER TABLE ... RENAME`, `DROP TABLE` via SQL | no rename; re-tag `TypeName` | file delete only; no rename |
| Transactions | `Direct` mode only | sessions (replica set) | connection-scoped, reliable | partial / backend-dependent | none |
| Serializer that defines field shape | `CustomEntityMapper` | Mongo conventions + serializers | `EntityJsonSerializer` | `EntityJsonSerializer` | MessagePack composite resolver |

### 3.3 Identity semantics

`Entity.Id` is always a normalized 24-char lower-hex ObjectId string (`Entity.TryParseId`, `Entity.cs:10-33`). Generation is `EntityIdGenerator.GenerateNewId()` (`GoLive.Saturn.Data.Entities`). Storage differs (ObjectId in BSON providers, string in JSON providers, `EntityId` key in Stellar), but the logical id is uniform. This is the single most important simplifying fact for the port.

### 3.4 Discrepancies a migration engine must not paper over

- **Soft-delete visibility is inconsistent and LiteDbX is wrong.** The required semantic is uniform and simple: **a document is deleted if and only if `IsDeleted == true`; missing or false means not deleted.** Mongo already behaves this way (`exists(IsDeleted, false) OR IsDeleted == false`). LiteDbX does not: its filter is the strict `IsDeleted == false`, so a legacy document that predates the field is silently treated as deleted. This must be flipped (see §3.6, F1); the store adapter additionally exposes an explicit `includeDeleted` scan mode (§7.5).
- **`Properties` (`_p`) persistence is inconsistent and LiteDbX drops it.** Mongo stores `Properties` as `_p`; SQLite/DocumentDb/Stellar persist it as `Properties`; LiteDbX ignores it entirely (`EntityMapper.cs:15-17`), so any document written through `LiteDbRepository` loses its property bag. The required behavior is uniform: **persist `Properties` as `_p` whenever it is non-null and contains at least one item; do not write `_p` when null or empty.** This must be fixed in LiteDbX (see §3.6, F2) before the migration engine can treat the field as storage-backed. The `RemoveFieldWhen("Properties", EmptyDocument)` cleanup then behaves identically everywhere.
- **Field casing differs**: BSON providers use BSON element names (`_id`, `_v`, `_p`, configured conventions); JSON providers emit PascalCase CLR property names (`Id`, `Version`, `Properties`, `IsDeleted`). Paths in migration definitions therefore cannot be one literal string set across providers unless the adapter performs a **field-name mapping**. See §7.3.
- **Index identity differs**: only Mongo exposes a true index manager; SQLite has `sqlite_master`; LiteDbX has none public despite the engine storing `$indexes`; DocumentDb creates indexes by reflection; Stellar has none.

### 3.5 Bootstrapping problem

A legacy database being repaired by this engine predates the current Saturn entity shape. The migration must therefore operate on **raw storage**, not on deserialized entities: legacy documents may not deserialize into today's `Entity` subclasses. This rules out "just use `IRepository.All<T>()` and re-save" as the engine substrate. The raw handle is mandatory.

### 3.6 Prerequisite provider correctness fixes

These are not migration-engine features; they are corrections to provider behavior that the port depends on. They land in Phase 0 and gate Phase 1.

**F1 — Uniform soft-delete semantics (LiteDbX).**
Define "deleted" as `IsDeleted == true`; missing or false means not deleted, on every provider. Change LiteDbX's not-deleted predicate from the strict `IsDeleted == false` to the negative form (`not (IsDeleted == true)`, i.e. `$not`/`!= true`) so documents lacking the field are visible. Update `LiteDbRepository.BuildNotDeletedPredicate` (`LiteDbRepository.cs:304-316`) and its callers (`ApplySoftDeleteFilter`). `Restore`/`Delete` still set the field explicitly, so the change only affects legacy/missing-field documents. Mongo, SQLite, DocumentDb and Stellar already implement this semantic; add a shared conformance test that asserts it across all providers, including a fixture document with `IsDeleted` absent.

**F2 — Persist `Properties` as `_p` (LiteDbX).**
`CustomEntityMapper` currently ignores `Properties` (`EntityMapper.cs:15-17`), so the property bag never reaches disk. Map it to `_p` symmetrically to Mongo:
- Serialize: when `Properties != null && Properties.Count > 0`, write the dictionary to `_p`; otherwise omit the field.
- Deserialize: read `_p` back into `Properties`; missing or null yields an empty dictionary (never null).
- Keep `Changes` and `EnableChangeTracking` ignored (not persisted) as today.
Add round-trip tests: empty/non-empty/missing `_p`, and confirm the on-disk element name is `_p` so the §7.3 alias map and cross-provider migrations work.

**F2b — Confirm parity of the other providers.**
Mongo already maps `Properties`→`_p` (`MongoDbRepository.cs:447`); SQLite/DocumentDb/Stellar persist `Properties` verbatim. Only field-name normalization differs (§7.3). No change required beyond tests.

---

## 4. Porting strategy options

### Option A — Reuse LiteDbX.Migrations verbatim for LiteDbX only

Reference `LiteDbX.Migrations` from `Saturn.Data.LiteDbX`, call `database.Migrations()` through the existing `protected LiteDatabase`.

- Pros: zero engine work for LiteDbX; exact behavior.
- Cons: solves one provider; DSL/report types diverge from anything built for other providers; the `protected` handle still needs surfacing; no path to Mongo/SQLite/etc. Rejected as the overall answer, retained as a **bootstrap fallback** for Phase 1 risk.

### Option B — One materialized canonical model (e.g. `MongoDB.Bson.BsonDocument`) for all providers

Convert every provider's storage into a single BSON tree, run the original engine, convert back.

- Pros: largest possible reuse; one navigator.
- Cons: forces a lossy, convention-heavy BSON↔JSON bridge for SQLite/DocumentDb and an impossible one for Stellar's MessagePack; risks ObjectId/DateTime/int-width corruption; adds a hard dependency on MongoDB.Bson to JSON-only deployments; still doesn't solve shared-table or no-rename providers. Rejected.

### Option C — One engine over an abstract mutable document tree, per-provider `IMigrationStore` — **CHOSEN**

Define a small mutation abstraction (`MigrationValue` / `MigrationObject` / `MigrationArray`), port the navigator/predicates/operations to it once, and implement an adapter per provider that either wraps the native tree (BSON/JSON) or materializes one.

- Pros: one engine, one DSL, one report; adapters keep native fidelity; no forced format conversion; capability-aware; testable with an in-memory adapter; no forced dependency on any provider package in the core.
- Cons: the port is mechanical but real work; the abstraction must cover array/object mutation and typed leaves; Stellar still needs a special decision.
- **Chosen.** Option A remains on the table only as a Phase 1 bootstrap fallback if core extraction slips; Option B is rejected.

---

## 5. Target architecture (Option C)

Three layers, plus one authoring surface.

```
+---------------------------------------------------------------+
| Authoring surface                                             |
|   repository.Migrations() / store.Migrations()                |
|   .Migration("name", m => m.ForCollection("*", c => ...))     |
|   .WithBackupRetention(...).RunAsync()                        |
+---------------------------------------------------------------+
| Engine  (GoLive.Saturn.Data.Migrations)                       |
|   MigrationRunner, MigrationBuilder, CollectionMigrationBuilder|
|   DocumentPathNavigator, MigrationPredicates                  |
|   operations, reports, progress, run options                  |
|   journal + id-remap + backup stores (via IMigrationStore)    |
+---------------------------------------------------------------+
| Storage abstraction                                           |
|   IMigrationStore, IMigrationCollection, capabilities         |
+---------------------------------------------------------------+
| Adapters (one per provider, inside provider assembly)         |
|   LiteDbXMig/ MongoMigrationStore/ SqliteMigrationStore/      |
|   DocumentDbMigrationStore/ StellarMigrationStore(limited)    |
+---------------------------------------------------------------+
```

### 5.1 Document abstraction (core)

The abstraction mirrors the BSON value model that the original navigator already assumes, so the port is close to a type substitution. It is deliberately minimal and allocation-light; the builders can wrap native nodes or materialize.

```csharp
namespace GoLive.Saturn.Data.Migrations;

public enum MigrationValueKind
{
    Null, Object, Array, String, Boolean,
    Int32, Int64, Double, Decimal, DateTime,
    ObjectId, Guid, Binary, MinValue, MaxValue
}

public abstract class MigrationValue
{
    public abstract MigrationValueKind Kind { get; }
    public bool IsNull => Kind == MigrationValueKind.Null;
    public bool IsObject => Kind == MigrationValueKind.Object;
    public bool IsArray => Kind == MigrationValueKind.Array;
    public virtual MigrationObject AsObject() => throw new InvalidOperationException();
    public virtual MigrationArray AsArray() => throw new InvalidOperationException();
    public virtual string AsString => throw new InvalidOperationException();
    public virtual MigrationObjectId AsObjectId => throw new InvalidOperationException();
    public virtual long AsInt64 => throw new InvalidOperationException();
    public virtual bool AsBoolean => throw new InvalidOperationException();
    public virtual DateTime AsDateTime => throw new InvalidOperationException();
    public virtual Guid AsGuid => throw new InvalidOperationException();
    public virtual byte[] AsBinary => throw new InvalidOperationException();
}

public abstract class MigrationObject : MigrationValue
{
    public abstract int Count { get; }
    public abstract bool TryGet(string name, out MigrationValue value);
    public abstract void Set(string name, MigrationValue value);
    public abstract bool Remove(string name);
    public abstract IEnumerable<KeyValuePair<string, MigrationValue>> Elements { get; }
}

public abstract class MigrationArray : MigrationValue
{
    public abstract int Count { get; }
    public abstract MigrationValue this[int index] { get; set; }
    public abstract void Add(MigrationValue value);
    public abstract void RemoveAt(int index);
}

public readonly struct MigrationObjectId : IEquatable<MigrationObjectId>
{
    public MigrationObjectId(string hex);
    public IReadOnlyList<byte> Bytes { get; }
    public override string ToString();
}
```

Adapters (thin): `BsonMigrationObject` over `LiteDbX.BsonDocument`, `MongoBsonMigrationObject` over `MongoDB.Bson.BsonDocument`, `JsonMigrationObject` over `System.Text.Json.Nodes.JsonObject`, and an optional `MessagePackMigrationObject` over a contractless map.

### 5.2 Storage abstraction (core)

```csharp
public interface IMigrationStore
{
    IMigrationStoreCapabilities Capabilities { get; }
    string IdFieldName { get; }                         // "_id" for BSON, mapped for JSON
    IAsyncEnumerable<string> GetCollectionsAsync(CancellationToken cancellationToken = default);
    bool CollectionExists(string collection);
    IMigrationCollection GetCollection(string collection);
    Task<bool> RenameCollectionAsync(string source, string target, CancellationToken cancellationToken = default);
    Task<bool> DropCollectionAsync(string collection, CancellationToken cancellationToken = default);
}

public interface IMigrationCollection
{
    string Name { get; }
    string IdFieldName { get; }
    IReadOnlyList<MigrationIndexDefinition> GetIndexes();
    IAsyncEnumerable<MigrationObject> ScanAsync(bool includeDeleted, CancellationToken cancellationToken = default);
    Task InsertAsync(MigrationObject document, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(MigrationObject document, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(MigrationValue id, CancellationToken cancellationToken = default);
    Task EnsureIndexAsync(MigrationIndexDefinition index, CancellationToken cancellationToken = default);
}

public sealed record MigrationIndexDefinition(string Name, string Expression, bool Unique);

public sealed class MigrationStoreCapabilities
{
    public bool SupportsRawDocuments { get; init; }
    public bool SupportsRebuild { get; init; }
    public bool SupportsRenameCollection { get; init; }
    public bool SupportsIndexEnumeration { get; init; }
    public bool SupportsTransactions { get; init; }
    public bool SupportsObjectIdOnDisk { get; init; }
    public bool SupportsIncludeDeleted { get; init; }
}
```

The runner consults capabilities before choosing in-place vs rebuild, and fails fast (with a clear message) when a definition requires an unsupported operation. This is how "all providers" stays honest.

### 5.3 Entry point / authoring surface

Add a marker interface to the abstraction layer and implement it per provider:

```csharp
namespace GoLive.Saturn.Data.Abstractions;

public interface IMigrationStoreSource
{
    IMigrationStore CreateMigrationStore();
}
```

Then a single extension, mirroring `db.Migrations()`:

```csharp
public static class MigrationStoreExtensions
{
    public static MigrationRunner Migrations(this IMigrationStore store)
        => new MigrationRunner(store);
}
```

Usage stays almost identical to LunarFlare:

```csharp
await using var repository = new LiteDbRepository(new RepositoryOptions(), new LiteDBRepositoryOptions { ConnectionString = args[0] });
var report = await repository.CreateMigrationStore().Migrations()
    .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
    {
        c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
        c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
    }))
    .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
    .RunAsync();
```

`ForCollection` selects **logical collections** (provider collection names, i.e. `RepositoryOptions.GetCollectionName(type)`), not physical tables. A later convenience overload can be added: `ForEntity<T>(...)`, resolving the name through the provider options.

### 5.4 Journal, id-remap and backup storage

These are infrastructure collections, implemented on top of `IMigrationStore` itself so they are provider-agnostic:

- Journal: reserved logical collection `__saturn_migrations`, one document per migration name, containing the ported `MigrationExecutionResult` fields (`appliedUtc`, `runId`, counts).
- Id remap: reserved logical collection `__saturn_migration_id_mappings`, documents as today (`migrationName`, `runId`, `sourceCollection`, `oldIdRaw`, `oldIdType`, `newObjectId`, `policy`, `documentOrdinal`).
- Backups/shadows: `{collection}__backup__{runId8}` / `{collection}__migrating__{runId8}` as logical collection names. The selector excludes them.

For BSON providers these are real extra collections. For SQLite they are real extra tables (the adapter creates them with the canonical `_doc` schema or a dedicated small schema). For DocumentDb they must live as a second `TypeName` (`__saturn_migrations`) inside the shared table. For Stellar journaling is possible as extra MessagePack files but rebuild is not, so journaling is still useful.

### 5.5 Project layout

```
src/
  Saturn.Data.Migrations/                         <- new core, net10.0, references Saturn.Data.Entities + Abstractions
    Abstractions/ (IMigrationStore, IMigrationCollection, capabilities, value model)
    Documents/    (DocumentPathNavigator, MigrationPredicates, MigrationPredicateContext)
    Engine/       (MigrationRunner, MigrationBuilder, CollectionMigrationBuilder, ops)
    Reporting/    (MigrationReport, MigrationExecutionResult, ...)
    Infrastructure/ (JournalStore, IdRemapStore, BackupManager)
    MigrationStoreExtensions.cs

  Saturn.Data.Migrations.Tests/                   <- new; unit + an in-memory IMigrationStore

  Saturn.Data.LiteDbX/.../Migrations/             <- adapter: LitDbxMigrationStore (+ IMigrationStoreSource on LiteDbRepository)
  Saturn.Data.MongoDb/.../Migrations/             <- adapter: MongoMigrationStore
  Saturn.Data.Sqlite/.../Migrations/              <- adapter: SqliteMigrationStore
  Saturn.Data.DocumentDb/.../Migrations/          <- adapter: DocumentDbMigrationStore
  Saturn.Data.Stellar/.../Migrations/             <- adapter: StellarMigrationStore (limited) or explicit NotImplemented
```

Adapters live in provider assemblies to reach `protected`/`internal` handles and reuse the exact serializers/mappers. New solution folders `Migrations/` and per-provider `Migrations` subfolders are added to `Saturn.Data.slnx`.

### 5.6 Type port mapping

| LiteDbX.Migrations | Saturn.Data.Migrations |
| --- | --- |
| `BsonPathNavigator` | `DocumentPathNavigator` over `MigrationObject`/`MigrationValue` |
| `BsonValue` / `BsonDocument` / `BsonArray` | `MigrationValue` / `MigrationObject` / `MigrationArray` |
| `BsonType` | `MigrationValueKind` |
| `BsonPredicateContext` / `BsonPredicate` | `MigrationPredicateContext` / `MigrationPredicate` |
| `BsonPredicates` | `MigrationPredicates` (same catalog + composition) |
| `InvalidObjectIdPolicy` | unchanged name |
| `CollectionMigrationBuilder`, `ReferenceRepairBuilder`, `FieldConversionBuilder`, `IdConversionBuilder` | unchanged (retargeted) |
| `MigrationBuilder`, `MigrationDefinition`, `CollectionMigrationPlan` | unchanged |
| `MigrationRunner` | `MigrationRunner(IMigrationStore)` |
| `MigrationHistoryStore` | `JournalStore(IMigrationStore)` |
| `IdRemapLog`, `IdRemapEntry` | unchanged shape over `IMigrationStore` |
| `MigrationReport`, `MigrationExecutionResult`, `CollectionMigrationResult`, `RebuildValidationSummary`, `MigrationProgress`, `MigrationRunOptions` | copied verbatim (no provider coupling) |
| `LiteDatabaseMigrationExtensions.Migrations()` | `MigrationStoreExtensions.Migrations()` |

The runner's `$indexes` read (`MigrationRunner.cs:587-601`, the one direct LiteDbX-engine assumption) is replaced by `IMigrationCollection.GetIndexes()`.

---

## 6. Per-provider adapter design

### 6.1 LiteDbX adapter (Tier 1 — full)

- Store wraps the repository's `LiteDatabase`. Add `IMigrationStoreSource` to `LiteDbRepository` returning a store over the existing handle (the repository already owns it; no second open, no static-mapper mutation).
- `GetCollectionsAsync` → `database.GetCollectionNames()`.
- `GetCollection(name)` → `database.GetCollection(name, BsonAutoId.ObjectId)`; `MigrationObject = BsonMigrationObject`.
- `ScanAsync` → `collection.FindAll()` (include deleted is inherent: no filter is applied at raw level).
- `Insert/Update/Delete` → `Insert`, `Update`, `Delete` on `ILiteCollection<BsonDocument>`.
- Rebuild → `RenameCollection` + `DropCollection` already exist on the engine and are exact.
- Caps: raw ✔, rebuild ✔, rename ✔, index enumeration ✘ (none; `GetIndexes` returns empty unless we persist definitions — see §8.4), transactions ✔ only in `Direct` mode, ObjectId-on-disk ✔.
- Field names are BSON-native (`_id`, etc.); no mapping needed.
- Caveat: `ILiteCollection<BsonDocument>` returns `BsonAutoId.ObjectId`-generated ids; `Insert` of a doc that already carries `_id` is honored.

### 6.2 MongoDb adapter (Tier 1 — full)

- Store wraps `MongoDatabase`. Add `IMigrationStoreSource` to `MongoDbRepository`.
- `GetCollection(name)` → `mongoDatabase.GetCollection<MongoDB.Bson.BsonDocument>(name)`.
- Scan → `Find(FilterDefinition<BsonDocument>.Empty)` (optionally `{ IsDeleted: { $ne: true } }` when `includeDeleted == false`).
- Update/Delete by `_id` (ObjectId) or by raw filter; insert via `InsertOne`.
- Rebuild → driver `RenameCollection` / `DropCollection` (rename across collections is supported server-side).
- Index enumeration → `collection.Indexes.ListAsync()` mapped to `MigrationIndexDefinition`.
- Caps: all true (transactions require replica set; document that).
- Field names BSON-native; `_id` is ObjectId; `Ref` is ObjectId scalar; both match the engine's assumptions directly.

### 6.3 Sqlite adapter (Tier 1 — full)

- Store wraps the repository's `SqliteConnectionFactory` + `knownTables`.
- Logical collection → table `"{collectionName}"` created by `EnsureTableAsync` schema `(_id, _v, _deleted, _scope, _scope2, _archived, _doc)`.
- `GetCollectionsAsync` → `SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'` (after `InitializeAsync`).
- `ScanAsync` → `SELECT _id, _doc FROM "{table}" [WHERE _deleted = 0]`, deserialize `_doc` with the repository's `EntityJsonSerializer.JsonOptions` into a `JsonMigrationObject`. The adapter must **normalize the id**: expose the JSON `Id` (and the `_id` column) under the engine's canonical `_id` field, and write it back to both on persist.
- `UpdateAsync` → serialize the `JsonObject` back to `_doc` and recompute the projection columns exactly as the provider does (`_v = json_extract($.Version)`, `_deleted = COALESCE($.IsDeleted,0)`, `_scope`, `_scope2`, `_archived`) in the same `UPDATE` shape used by `Repository.cs`. Reusing the provider's own write helper (or extracting it) is strongly preferred over duplicating the SQL.
- `DeleteAsync` → `DELETE FROM "{table}" WHERE _id = @id`.
- Rebuild → `CREATE TABLE "{shadow}" (...)` (reuse `EnsureTableAsync`), `INSERT ... SELECT`, then `ALTER TABLE "{source}" RENAME TO "{backup}"`, `ALTER TABLE "{shadow}" RENAME TO "{source}"`. Index names must be re-created after swap (see §8.4) and `knownTables` must be invalidated.
- Caps: raw ✔, rebuild ✔, rename ✔ (SQL), index enumeration ⚠ (via `PRAGMA index_list`/`index_info`, must be added), transactions ✔ (connection-scoped), ObjectId-on-disk ✘ (JSON string; `SupportsObjectIdOnDisk=false`).
- Field names are PascalCase JSON. See §7.3 for the mapping rule.

### 6.4 DocumentDb adapter (Tier 2 — shared table)

This provider is the schema outlier: **one physical table `documents`**, all types discriminated by a `TypeName` column set to `instance.GetType().Name` (Shiny) / `EntityJsonSerializer`.

- Logical collection → the `TypeName` value (i.e. `GetCollectionName(type)`).
- `GetCollectionsAsync` → `SELECT DISTINCT TypeName FROM documents` via the supplied `DatabaseProvider` or the raw `IDocumentStore`.
- `GetCollection(typeName)` scans `Data` JSON filtered by `TypeName`.
- Rebuild cannot rename a table. Strategy: write the repaired documents under a temporary discriminator (e.g. `{type}__migrating__{runId8}`), then in a single maintenance pass rewrite `TypeName` for all rows of `{type}__migrating__...` back to `{type}` and delete the original rows. This is a logical swap, not a physical one; it must run with the provider quiesced. Backups are the same pattern with a `__backup__` discriminator (retrievable via a `TypeName LIKE ...` scan).
- Indexes: the provider creates indexes by reflection over key paths; adapter can attempt `CreateIndexAsync` reuse and enumerate via `BuildListAllIndexesSql` if surfaced.
- Caps: raw ✔ (JSON), rebuild ⚠ via discriminator re-tag, rename ✘, index enumeration ⚠, transactions ⚠ (backend-dependent, `CommitAsync` does not commit explicitly begun Shiny transactions per the provider report), ObjectId-on-disk ✘.
- Note `DocumentDbRepositoryOptions.Store` makes raw access possible; if not supplied, the adapter must construct its own `DocumentStore` over the same `DatabaseProvider`/table to guarantee identical `JsonSerializerOptions`.

### 6.5 Stellar adapter (Tier 3 — limited or unsupported)

FastDB stores typed MessagePack values keyed by `EntityId`; there is no JSON blob, no raw string lane, no transactions, no indexes, and no rename API. Two sub-options:

- **3a (recommended for scope): declare raw migrations unsupported.** `StellarMigration` throws a clear `NotSupportedException` from `CreateMigrationStore()`, or returns a store with `SupportsRawDocuments = false` so the runner fails per-definition with an actionable message. Stellar users remain on entity-typed code or offline repair.
- **3b (late, best-effort):** materialize a `MessagePackMigrationObject` by deserializing the record into a contractless `Dictionary<string, object>` tree, mutate, re-serialize. This only works while legacy documents remain deserializable to a map (not to today's entity), and rebuild would require closing the FastDB collection, renaming the `{name}.db` file, and reopening — fragile and not transaction-safe. Defer and gate behind explicit opt-in.

Either way, the capability matrix makes the limitation explicit instead of failing at runtime with an opaque error.

---

## 7. Semantic decisions to lock down

### 7.1 "Collection" means logical collection

The engine's `ForCollection(selector)` matches logical collection names (provider `GetCollectionName(type)`), never physical tables. This is the only definition that works across all five providers and keeps `ForCollection("*")` meaningful. Documented deviation from LiteDbX, where collection == physical collection (mostly the same for LiteDbX/Mongo; different for DocumentDb).

### 7.2 `_id` canonicalization

Every adapter exposes the document identity under the canonical field name `_id` in the materialized tree, mapping to/from the provider's storage identity (`_id` BSON, `Id` JSON/`_id` column, `EntityId` key). This preserves the engine's `ConvertId()` path and the `RepairReference("...$id")` semantics. `IMigrationStore.IdFieldName` records the physical name for diagnostics.

### 7.3 Field-name mapping (case/convention)

BSON providers use mixed element names. Mongo maps `Properties`→`_p` and `Version`→`_v`; LiteDbX maps `Properties`→`_p` (after F2, §3.6) but keeps `Version` and most CLR names; JSON providers persist PascalCase CLR names (`Properties`, `Version`, `IsDeleted`). Migration authors will naturally write Saturn property names (`Properties`, `SecondScope`, `Scope`, `Scopes`, `Permissions`, `Groups`, `ParentItemId`, `Payload`). Two mitigations, combine them:

1. Adapters apply a **logical↔physical name map** on entry/exit. E.g. `BsonMigrationObject` maps `_id`↔`_id`, and known Saturn element aliases (`Properties`↔`_p`, and `Version`↔`_v` for Mongo) both ways. Anything unmapped is passed through verbatim. F2 guarantees LiteDbX actually writes and reads `_p`, so the alias is not a no-op there.
2. For provider-specific legacy fields that only exist on one provider, expose `ForCollection` per provider in a **provider-scoped migration assembly** rather than trying to make one definition universal.

The port must document that migration definitions are **field-name-physical per provider family** unless written against the logical aliases. The LunarFlare migration (`SecondScope`, `Scope`, `Scopes[*]`, `Permissions[*]`, `Groups[*]`, `ParentItemId`, `Payload`, `Properties`) is written in Saturn CLR-property names and therefore maps cleanly to JSON providers and, via aliases, to BSON providers.

### 7.4 Rebuild atomicity and backups

Original engine: shadow write → replay indexes → rename source→backup → rename shadow→source; on failure restore. Port preserves the algorithm but is capability-gated:

- `SupportsRenameCollection` → true swap.
- DocumentDb → discriminator re-tag swap (documented as non-atomic; requires maintenance window).
- Stellar → unsupported.
- If `SupportsTransactions` and `BackupRetentionPolicy == DeleteOnSuccess`, wrap the swap in a transaction where the provider allows; otherwise proceed backup-first and treat the backup as the recovery point. This mirrors the cascade proposal's "Stellar = best effort + compensation log" stance.

### 7.5 Soft-delete visibility

After F1 the semantic is uniform: **a document is deleted if and only if `IsDeleted == true`; missing or false means not deleted**, on every provider. A migration definition therefore never needs provider-specific knowledge.

`ScanAsync(includeDeleted)` stays explicit:

- default (`false`) — skip documents whose `IsDeleted == true`;
- `.IncludeDeleted()` — scan every document, including tombstoned rows (needed when repairing ids or compacting properties on deleted data).

The store applies the filter, not the migration predicate, so the choice is consistent across LiteDbX, Mongo, SQLite, DocumentDb and (where supported) Stellar. A document that predates the `IsDeleted` field is always treated as not deleted.

### 7.6 ObjectId conversion semantics per storage

`ConvertId().FromStringToObjectId()` and `ConvertField(...).FromStringToObjectId()` mean different physical things per provider:

- LiteDbX/Mongo: rewrite legacy `string` BSON element to `ObjectId` (the original behavior).
- SQLite/DocumentDb: the id/reference is already a 24-hex JSON string; the operation becomes a **normalization/validation** pass — parse the string, and on invalid apply the policy (`GenerateNewId` rewrites the string). `SupportsObjectIdOnDisk=false` is informational; the operation still works.
- Stellar: key is `EntityId`; invalid non-hex ids cannot even be keyed, so this becomes a read-time repair or is unsupported.

`RepairReference` similarly operates on `MigrationObjectId` values; the adapter decides the physical representation (ObjectId vs string).

---

## 8. Capability matrix and tiering

### 8.1 Feature matrix

| Feature | LiteDbX | MongoDb | Sqlite | DocumentDb | Stellar |
| --- | --- | --- | --- | --- | --- |
| Raw scan/mutate | ✅ | ✅ | ✅ | ✅ | ⚠️ map-only |
| In-place migration | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| `ConvertId` rebuild | ✅ | ✅ | ✅ | ✅ re-tag | ❌ |
| Reference repair | ✅ | ✅ | ✅ | ✅ | ❌ |
| Rename/swap collection | ✅ | ✅ | ✅ | ⚠️ logical | ❌ |
| Index replay | ❌ enumerate | ✅ | ⚠️ add PRAGMA | ⚠️ | ❌ |
| Transactions | ⚠️ Direct | ✅* | ✅ | ⚠️ | ❌ |
| Backups/retention | ✅ | ✅ | ✅ | ✅ logical | ❌ |
| Journal + id-map | ✅ | ✅ | ✅ | ✅ | ⚠️ |

`*` Mongo transactions require a replica set.

### 8.2 Tiers

- **Tier 1 — full (LiteDbX, MongoDb, Sqlite):** all operations including rebuild/swap, backups, id-remap, reference repair.
- **Tier 2 — in-place + logical rebuild (DocumentDb):** raw repair and reference-repair work; rebuild is a discriminator re-tag under a maintenance window; indexes best-effort.
- **Tier 3 — limited (Stellar):** raw/rebuild unsupported by default; store returns `SupportsRawDocuments=false`; only explicitly-supported in-place operations may run if 3b is implemented later.

---

## 9. Phased delivery plan

### Phase 0 — Provider prerequisite fixes + core extraction

Provider fixes (F1/F2, §3.6), land first and independently testable:
- F1: LiteDbX not-deleted predicate flipped to "not (`IsDeleted == true`)"; missing field is not deleted. Cross-provider conformance test added.
- F2: LiteDbX `EntityMapper` maps `Properties`↔`_p`, writing only when non-empty and always reading back to a non-null dictionary. Round-trip tests added.
- F2b: parity assertions for Mongo/SQLite/DocumentDb/Stellar `Properties` shape.

Core deliverables:
- `Saturn.Data.Migrations` with the value model, `DocumentPathNavigator`, `MigrationPredicates`, all operations/builders, `MigrationRunner`, reports, options, progress.
- `IMigrationStore` / `IMigrationCollection` / capabilities.
- In-memory `IMigrationStore` test double.
- Ported unit tests from `LiteDbX.Migrations.Tests` (`MigrationRunner_Tests.cs`, `BsonPredicates_Tests.cs`) re-targeted to the abstraction.

Exit criteria: F1/F2 tests and the cross-provider soft-delete/`_p` conformance suite pass; navigator/predicate/operation parity tests pass against the in-memory store; no provider package referenced by core.

### Phase 1 — LiteDbX adapter + parity

Deliverables:
- `LiteDbxMigrationStore` inside `Saturn.Data.LiteDbX`; `IMigrationStoreSource` on `LiteDbRepository`.
- A sample migrator reproducing the LunarFlare `Program.cs` migration end-to-end against a copy of real data.
- Journal + id-remap + backup retention.
- Fallback: if core extraction slips, reference `LiteDbX.Migrations` directly through the surfaced handle for this provider only.

Exit criteria: the two LunarFlare migrations produce the same result and the same report as the reference implementation, on a database that includes legacy docs with missing `IsDeleted` and with both empty and non-empty `Properties`.

### Phase 2 — MongoDb adapter

- `MongoMigrationStore`; `IMigrationStoreSource` on `MongoDbRepository`.
- Native `_id` ObjectId, `_p`/`_v` aliases, driver rename/drop, index enumeration.
- Transactional swap where replica set available.

Exit criteria: shared contract test suite (below) passes on Mongo, including index replay.

### Phase 3 — Sqlite adapter

- `SqliteMigrationStore`; `IMigrationStoreSource` on `SqliteRepository`.
- Reuse `EnsureTableAsync` schema; projection-column maintenance; raw `PRAGMA index_list` enumeration; `ALTER TABLE` swap; `knownTables` invalidation.

Exit criteria: contract suite passes on SQLite; index names reconcile after swap.

### Phase 4 — DocumentDb adapter

- `DocumentDbMigrationStore`; `IMigrationStoreSource` on `DocumentDbRepository`.
- TypeName-based logical collections, discriminator re-tag rebuild, `Store`/`DatabaseProvider` reuse for identical serialization.
- Explicit maintenance-window contract; no atomic guarantee.

Exit criteria: contract suite passes with rebuild marked non-atomic; capabilities reported correctly.

### Phase 5 — Stellar decision + CLI

- Implement 3a (explicit unsupported with actionable error) by default; optionally 3b behind opt-in.
- A `Saturn.Data.Migrations.Cli` (or extend the pattern of `GoLive.LunarFlare.LiteDbxMigrator`) that takes a provider name + connection string + migration assembly, mirroring the reference Program.

Exit criteria: `--provider stellar` fails with a documented message; `--provider litedbx|mongodb|sqlite` apply the LunarFlare migration.

### Phase 6 — Hardening and docs

- Long-running/streaming behavior on large collections; progress callback parity.
- Fuzz the path parser; snapshot the predicate catalog.
- User guide (`docs/migrations.md`) and per-provider field-name/convention appendix.

### Cross-cutting workstreams

- Journal/id-remap/backups as store-level infrastructure.
- Capability negotiation and fail-fast diagnostics.
- DI registration per provider (`ServicesExtensions`) that registers `IMigrationStoreSource`.

---

## 10. Testing strategy

1. **Contract test suite** (`Saturn.Data.Migrations.Tests.Shared`): one parameterized suite run against every provider that can host it. Cases: id conversion (all three accepted policies), invalid-value sampling, field add/set/remove/modify, nested + wildcard + recursive paths, prune-empty-parents, transfers, `RemoveWhere(UselessValueAggressive, Recursive)`, seed insert, reference repair across collections, duplicate-target-id detection, dry-run reporting, journal idempotency, backup retention and cleanup, progress stage order.
2. **Store conformance tests** per adapter: enumeration, include-deleted filtering, id mapping, casing alias mapping, index enumeration, swap semantics.
3. **Parity harness**: run the LunarFlare migration fixture through the old LiteDbX.Migrations and the new LiteDbX adapter, assert deep-equal resulting documents and report counts.
4. **Legacy-data fixtures**: hand-built old-format databases (string `_id`, `Properties` docs, null `Payload`, mixed-shape documents) per provider, per the reference tests.
5. **Failure injection**: swap failure mid-rebuild, index replay failure, duplicate ids in dry-run vs live.

Reuse the existing provider test fixtures (`DatabaseFixture`) and the `UnitTestable*Repository` subclasses that already expose raw handles.

---

## 11. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Porting ~4.4k LoC introduces behavior drift | High | Mechanical type substitution; port the existing tests before adapting; parity harness in Phase 1 |
| F1 soft-delete flip changes LiteDbX read results | High | It restores the intended semantic; audit existing read tests that relied on missing-field-as-deleted; add explicit conformance test |
| F2 `_p` persistence changes LiteDbX written bytes | Medium | Additive (previously dropped); verify no `_p` written for null/empty; round-trip tests |
| Field-name divergence BSON vs JSON | High | Explicit logical alias map per adapter; document physical-name authoring; per-provider definitions where needed |
| ObjectId/DateTime/number fidelity in JSON | Medium | Keep `Int32`/`Int64`/`Decimal` distinct in the value model; parse via `JsonElement`; never round-trip through a single number type; snapshot tests |
| DocumentDb shared table rebuild not atomic | High | Explicit maintenance window; backup-first; capability flag and clear runner error if quiescence cannot be guaranteed |
| Stellar cannot do raw/rebuild | Medium | Capability tier + actionable error; opt-in map mode deferred |
| Index enumeration missing (LiteDbX/SQLite/DocumentDb) | Medium | Persist index definitions in the migration journal when the provider's own manager created them; add `PRAGMA` enumeration to SQLite; otherwise documented as "indexes must be re-declared" |
| Transactions unavailable (Stellar/DocumentDb/LiteDbX Shared) | Medium | Capability-gated atomicity; backup-first fallback; never claim atomicity the provider cannot deliver |
| Internal handle access | Low | Adapters live in provider assemblies; add `IMigrationStoreSource` publicly |
| Soft-delete visibility mismatch | Medium | Explicit `includeDeleted` scan; store-level filter, not predicate |
| Concurrent writers during rebuild | Medium | Require quiescence; SQLite/Mongo locks; document operational contract |
| Core accidentally taking a provider dependency | Medium | Enforce via a dependency test/CI rule: core references only `Saturn.Data.Entities` + `Saturn.Data.Abstractions` |

---

## 12. Open questions

1. **Per-provider definitions vs universal definitions.** Do we commit to a single universal migration definition (with alias mapping), or accept that some migrations are provider-scoped and ship them in provider-specific assemblies? Recommended: universal for single-field/normalization work, provider-scoped for storage-specific repairs.
2. **Index persistence.** Should the core own an index registry written into the journal so LiteDbX/SQLite/DocumentDb rebuilds can replay indexes, or should each provider expose its own enumeration regardless of cost?
3. **Stellar:** hard-unsupported (3a) or attempt map-mode (3b) in this effort?
4. **Entry point shape.** `IMigrationStoreSource` on every repository, a standalone factory, or both? (Affects DI surface and whether migrations can run without constructing a full repository.)
5. **DocumentDb atomicity contract.** Accept logical swap under maintenance, or exclude DocumentDb from rebuild-tier entirely and only allow in-place operations?
6. **Where the CLI lives.** Extend `GoLive.LunarFlare.LiteDbxMigrator` or add a provider-agnostic `Saturn.Data.Migrations.Cli`?

---

## 13. Alternatives considered

- **Reuse `LiteDbX.Migrations` verbatim, per provider.** Fast for LiteDbX, but produces N divergent DSLs and N report shapes. Rejected as the primary strategy.
- **Canonicalize all providers to MongoDB.Bson and keep the original engine unchanged.** Forced lossy bridges for JSON and impossible for MessagePack; adds a heavy dependency to JSON/embedded deployments. Rejected.
- **Entity-typed migrations (`IRepository.All<T>()` + re-save).** Cannot read legacy documents that no longer deserialize into today's entity shape, which is the entire purpose of the tool. Rejected as the substrate, though useful for post-migration seeding (as LunarFlare already does).
- **Source-generated migration definitions.** Attractive for typed field names, but the input is legacy raw storage, not the current model; the DSL already solves this. Deferred.

---

## 14. Appendix A — LunarFlare migration ported

Reference (LiteDbX):

```csharp
await using (var db = await LiteDatabase.Open(args[0]))
{
    await db.Migrations()
        .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
        {
            c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
            c.RemoveFieldWhen("Properties", BsonPredicates.EmptyDocument);
        }))
        .Migration("20240411-OtherFieldsMigrations", m => m.ForCollection("*", c =>
        {
            c.ConvertField("SecondScope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.ConvertField("Scopes[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.ConvertField("Permissions[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.ConvertField("Groups[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.ConvertField("ParentItemId").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
            c.RemoveFieldWhen("Payload", BsonPredicates.Null);
        }))
        .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
        .RunAsync();
}
```

Saturn port (provider chosen by connection string / DI):

```csharp
await using var repository = MigrationRepositoryFactory.Create(provider, connectionString);
var report = await repository.CreateMigrationStore().Migrations()
    .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
    {
        c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
        c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
    }))
    .Migration("20240411-OtherFieldsMigrations", m => m.ForCollection("*", c =>
    {
        c.ConvertField("SecondScope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Scopes[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Permissions[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Groups[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("ParentItemId").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.RemoveFieldWhen("Payload", MigrationPredicates.Null);
    }))
    .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
    .RunAsync();
```

Only the entry point and predicate class name change. The field names are already Saturn CLR-property names, so no definition rewrite is needed; the alias map in §7.3 handles BSON element names.

---

## 15. Appendix B — Source inventory to port

Core (port, retarget):
- `BsonPathNavigator` → `DocumentPathNavigator`
- `CollectionMigrationBuilder` (+ `ReferenceRepairBuilder`, `FieldConversionBuilder`, `IdConversionBuilder`, operation classes)
- `MigrationRunner`
- `MigrationDefinition` (plans + reports)
- `MigrationCollectionSelector`
- `MigrationHistoryStore` → `JournalStore`
- `IdRemapLog`
- `MigrationRunOptions`, `MigrationProgress`
- `BsonPredicateContext` / `BsonPredicates`
- `DocumentMigrationExecutionContext`
- `CollectionInsertOperations`
- `InvalidObjectIdPolicy`
- `LiteDatabaseMigrationExtensions` → `MigrationStoreExtensions`

Copy verbatim (no provider coupling): `MigrationReport`, `MigrationExecutionResult`, `CollectionSelectorResult`, `CollectionMigrationResult`, `RebuildValidationSummary`, `SecondaryIndexReplayPlan`, `DuplicateTargetIdSample`, `InvalidValueSample`, `MigrationRunOptions`, `BackupRetentionPolicy`, `BackupDisposition`, `BackupCleanupOptions/Report/Result`, `MigrationProgress`.

New: value model adapters, `IMigrationStore`/`IMigrationCollection`, capabilities, `IMigrationStoreSource`, per-provider adapters, CLI, contract tests.

---

## 16. Appendix C — Worked example: a LunarFlare document before and after the migrations

This appendix shows the concrete effect of Appendix A on a real entity, `User : SecondScopedEntity<Account, Tenant>` (`GoLive.LunarFlare.Domain.User`), which carries `Scope` (`Ref<Tenant>`), `SecondScope` (`Ref<Account>`), `Scopes` (`List<string>`), `Groups` (`List<Ref<Group>>`), plus the legacy `Payload` field and the `Properties` bag. The migration does not edit C# classes; it rewrites the stored representation. Both storage dialects are shown because the same definition produces a different byte shape per provider.

### C.1 Before — legacy document on disk (raw, LiteDbX BSON)

Legacy data predates Saturn: ids are strings, `Properties` was written as `{}`, and `Payload` is a leftover column.

```json
{
  "_id": "67a92d08063e3290f03b29dc",
  "Version": 3,
  "Scope": "65da55b75278b72ba0ffb2de",
  "SecondScope": "6653a0b49f7645632fdaa5d7",
  "Scopes": ["65da55b75278b72ba0ffb2de", "6653a0b49f7645632fdaa5d7"],
  "Groups": ["66a5032dfa0b29da8989b525"],
  "Name": "Ada Lovelace",
  "ServiceUser": false,
  "Properties": {},
  "Payload": null
}
```

Second legacy variant, with a non-empty property bag:

```json
{
  "_id": "67a92d2a063e3290f03b29dd",
  "Version": 1,
  "Scope": "65da55b75278b72ba0ffb2de",
  "Properties": { "Theme": "dark", "Locale": "en-GB" }
}
```

### C.2 After — migrated document on disk

**LiteDbX / MongoDb (BSON).** `_id` and every reference become `ObjectId` scalars; array elements become `ObjectId`; a null `Payload` and an empty `Properties` are removed; a non-empty `Properties` becomes `_p` (F2) and survives.

```json
{
  "_id": ObjectId("67a92d08063e3290f03b29dc"),
  "Version": 3,
  "Scope": ObjectId("65da55b75278b72ba0ffb2de"),
  "SecondScope": ObjectId("6653a0b49f7645632fdaa5d7"),
  "Scopes": [ObjectId("65da55b75278b72ba0ffb2de"), ObjectId("6653a0b49f7645632fdaa5d7")],
  "Groups": [ObjectId("66a5032dfa0b29da8989b525")],
  "Name": "Ada Lovelace",
  "ServiceUser": false
}
```

```json
{
  "_id": ObjectId("67a92d2a063e3290f03b29dd"),
  "Version": 1,
  "Scope": ObjectId("65da55b75278b72ba0ffb2de"),
  "_p": { "Theme": "dark", "Locale": "en-GB" }
}
```

**Sqlite / DocumentDb (JSON).** Ids stay 24-hex strings (normalized/validated, not converted); `Payload` and empty `Properties` are removed; non-empty `Properties` is retained under its CLR name.

```json
{
  "Id": "67a92d08063e3290f03b29dc",
  "Version": 3,
  "Scope": "65da55b75278b72ba0ffb2de",
  "SecondScope": "6653a0b49f7645632fdaa5d7",
  "Scopes": ["65da55b75278b72ba0ffb2de", "6653a0b49f7645632fdaa5d7"],
  "Groups": ["66a5032dfa0b29da8989b525"],
  "Name": "Ada Lovelace",
  "ServiceUser": false
}
```

```json
{
  "Id": "67a92d2a063e3290f03b29dd",
  "Version": 1,
  "Scope": "65da55b75278b72ba0ffb2de",
  "Properties": { "Theme": "dark", "Locale": "en-GB" }
}
```

The only cross-provider difference is the physical field name (`_p` vs `Properties`) and the scalar type (BSON `ObjectId` vs JSON string); the alias map in §7.3 normalizes both so the migration definition is identical.

### C.3 After — the entity as loaded back in C#

The stored changes are invisible to consumers; Saturn returns a fully materialized entity with a populated property bag.

```csharp
var user = await repository.ById<User>("67a92d08063e3290f03b29dc");

user.Id;               // "67a92d08063e3290f03b29dc"
user.Version;          // 3
user.Scope.Id;         // "65da55b75278b72ba0ffb2de"   (Ref<Tenant>)
user.SecondScope.Id;   // "6653a0b49f7645632fdaa5d7"   (Ref<Account>)
user.Scopes;           // [ "65da55b75278b72ba0ffb2de", "6653a0b49f7645632fdaa5d7" ]
user.Groups[0].Id;     // "66a5032dfa0b29da8989b525"   (Ref<Group>)
user.Properties.Count; // 0  (empty bag was removed on disk, reconstructed as empty)

var themed = await repository.ById<User>("67a92d2a063e3290f03b29dd");
themed.Properties["Theme"]; // "dark"  (F2: _p round-trips instead of being dropped)
```

### C.4 After — the ported migrator program

The reference `Program.cs` becomes provider-selectable. `MigrationRepositoryFactory` is the illustrative helper introduced in Appendix A; it constructs the right repository from the provider name and registers `IMigrationStoreSource`.

```csharp
using GoLive.LunarFlare.Services;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Migrations;

if (args.Length < 2)
{
    Console.WriteLine("Usage: lunarflare-migrator <litedbx|mongodb|sqlite|documentdb> <connection-string>");
    return -1;
}

var provider = args[0];
var connectionString = args[1];

await using var repository = MigrationRepositoryFactory.Create(provider, connectionString);

var report = await repository.CreateMigrationStore().Migrations()
    .Migration("20260410-IDMigration", m => m.ForCollection("*", c =>
    {
        c.ConvertId().FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.GenerateNewId);
        c.RemoveFieldWhen("Properties", MigrationPredicates.EmptyDocument);
    }))
    .Migration("20240411-OtherFieldsMigrations", m => m.ForCollection("*", c =>
    {
        c.ConvertField("SecondScope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Scope").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Scopes[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Permissions[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("Groups[*]").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.ConvertField("ParentItemId").FromStringToObjectId().OnInvalidString(InvalidObjectIdPolicy.Fail);
        c.RemoveFieldWhen("Payload", MigrationPredicates.Null);
    }))
    .OnProgress(p => Console.WriteLine($"{p.Stage} {p.MigrationName}: {p.CompletedCollections}/{p.TotalCollections} collections, modified {p.DocumentsModified}"))
    .WithBackupRetention(BackupRetentionPolicy.DeleteOnSuccess)
    .RunAsync();

Console.WriteLine($"Migrations {report.Migrations.Count}, applied {report.Migrations.Count(m => m.WasApplied)}, modified {report.Migrations.Sum(m => m.DocumentsModified)}");

await repository.Rebuild();
await Seeder.CreateIfNotExists(repository);

return 0;
```

`repository.Rebuild()` is the provider compaction step (LiteDbX file rebuild, SQLite `VACUUM`, Mongo no-op); it is capability-gated and reported in `MigrationStoreCapabilities`. The seeding that follows is unchanged from the original program.

---

## 17. Implementation progress

Live tracker. Detailed resume notes live in `docs/litedbx-migrations-port-worklog.md`.

| Phase | Scope | Status |
| --- | --- | --- |
| 0 | F1/F2 provider fixes + core `GoLive.Saturn.Data.Migrations` extraction | done |
| 1 | LiteDbX adapter + parity | done |
| 2 | MongoDb adapter | done |
| 3 | Sqlite adapter | done |
| 4 | DocumentDb adapter | done (in-place only) |
| 5 | Stellar decision + CLI | pending |
| 6 | Hardening + docs | pending |

### Phase 0 checklist

- [x] F1 — LiteDbX soft-delete flip (`LiteDbRepository.BuildNotDeletedPredicate`); `Phase0PersistenceTests.SoftDelete_*` pass.
- [x] F2 — LiteDbX `Properties`⇄`_p` persistence (`EntityMapper`); empty bags omitted; round-trip test passes.
- [x] Core project + value model (`GoLive.Saturn.Data.Migrations`): `MigrationValue`/`MigrationObject`/`MigrationArray`/`MigrationObjectId`, `IMigrationStore`/`IMigrationCollection`/capabilities, `IMigrationStoreSource`.
- [x] `DocumentPathNavigator` port (paths, wildcards, recursive descent, add/replace/remove, prune).
- [x] `MigrationPredicates` port (full catalog + composition).
- [x] Operations + builders (`RemoveFieldWhen`, `RemoveWhere`, `AddFieldWhen`, `SetFieldWhen`, `ModifyFieldWhen`, `PruneEmptyContainers`, `ConvertField`, `ConvertId`).
- [x] `MigrationRunner` port (selector glob, in-place + rebuild/swap, journal, progress, dry-run, backup retention, id-remap log).
- [x] Reporting/options/progress port.
- [x] In-memory store test double + 11 tests green.

Deferred within Phase 0 (tracked in worklog): `RepairReference`/`InsertDocumentWhen` operations, backup cleanup (`CleanupBackupsAsync`/`KeepLatestCount`), duplicate-target-id detection, strict-path failure reporting details, index enumeration/replay.

### Phase 1 checklist

- [x] `LiteDbxMigrationStore` + `LiteDbxMigrationCollection` in `Saturn.Data.LiteDbX` (BSON⇄`MigrationObject` converter).
- [x] `IMigrationStoreSource` on `LiteDbRepository` (`CreateMigrationStore()`).
- [x] End-to-end test on real LiteDbX storage: legacy string `_id`/`Scope` → ObjectId, empty `Properties` + null `Payload` removed, rebuild/swap + journal.
- [x] Full LiteDbX suite green (60/60).

Note: LiteDbX exposes async storage APIs (`ValueTask`, `IAsyncEnumerable`); the adapter awaits them. `CollectionExists` is bridged synchronously (single lightweight call).

### Phase 2 checklist

- [x] `MongoMigrationStore` + `MongoMigrationCollection` in `Saturn.Data.MongoDb` (BSON⇄`MigrationObject` converter, `_id` ObjectId, driver rename/drop, index enumeration).
- [x] `IMigrationStoreSource` on `MongoDbRepository`.
- [x] End-to-end test on real MongoDB: legacy string `_id`/`Scope` → ObjectId, empty `Properties` + null `Payload` removed, rebuild/swap + journal.
- [x] Full MongoDb suite green (107/107).

Deferred: field-name alias map (`_p`↔`Properties`, `_v`↔`Version`) — migrations currently target provider-physical names (§7.3).

### Phase 3 checklist

- [x] `SqliteMigrationStore` + `SqliteMigrationCollection` in `Saturn.Data.Sqlite` (`_doc` JSON⇄`MigrationObject`, projection-column maintenance, table enumeration, `ALTER TABLE` rename, canonical index recreation, `knownTables` invalidation).
- [x] `IMigrationStoreSource` on `SqliteRepository` (`CreateMigrationStore()`; JSON ids stay strings, `SupportsObjectIdOnDisk=false`).
- [x] End-to-end test on a real SQLite file: canonical `_id`/`Id`, `Scope` string, empty `Properties`/null `Payload` removed, rebuild/swap.
- [x] Full Sqlite suite green (84/84).

### Phase 4 checklist

- [x] `DocumentDbMigrationStore` + `DocumentDbMigrationCollection` in `Saturn.Data.DocumentDb` over the Shiny raw JSON lane (`store.Collection(name, "Id")`); logical collection = `TypeName`.
- [x] `IMigrationStoreSource` on `DocumentDbRepository`.
- [x] Explicit collection selectors (`ForCollection("TypeName")`) supported by the runner even when a store cannot enumerate collections.
- [ ] Discriminator re-tag rebuild (`ConvertId`) — **deferred**; `SupportsRebuild=false`, so rebuild migrations fail fast on DocumentDb.
- [ ] Automated end-to-end test — **deferred**: raw `QueryStream` streaming did not complete reliably in the test host; adapter compiles and existing DocumentDb smoke suite is green, but raw migration behavior is unverified here.

DocumentDb therefore lands as **Tier 2 in-place only** for now (proposal §8.2), not the full re-tag rebuild.






Note: F2 additionally sets `CustomEntityMapper.DontSerializeEmptyCollections = true`; the full LiteDbX suite (59 tests) passes with F1+F2 applied.


