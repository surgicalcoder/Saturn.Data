# Proposal: A Shiny.DocumentDb-backed Provider (`GoLive.Saturn.Data.DocumentDb`)

**Status:** Draft for review — no phases yet
**Author:** Axiom
**Date:** 2026-09-28
**Scope:** Add a fifth repository provider to Saturn.Data that delegates storage and query translation to [Shiny.DocumentDb](https://github.com/shinyorg/DocumentDb), unlocking every backend it supports with one adapter.

---

## 1. Executive summary

Saturn.Data currently ships four providers: `MongoDb` (native driver), `LiteDbX` (native LiteDB), `Stellar` (embedded FastDB), and `Sqlite` (JSON1, hand-written SQL + expression translator).

This proposal adds **`GoLive.Saturn.Data.DocumentDb`** — a single adapter over `Shiny.DocumentDb`'s `IDocumentStore`. Shiny.DocumentDb is a schema-free JSON document store that runs on **SQLite, SQLCipher, LiteDB, MySQL, MariaDB, SQL Server, PostgreSQL, CockroachDB, Oracle (23ai+), DuckDB, Cosmos DB, MongoDB, Amazon DocumentDB, Redis Stack, RavenDB, Google Firestore, Azure Table Storage, Amazon DynamoDB, and IndexedDB (Blazor WASM)**.

The main point is the **server and cloud backends we cannot reach today**: PostgreSQL, SQL Server, MySQL/MariaDB, Oracle, CockroachDB, Cosmos DB, Redis, RavenDB, Firestore, Azure Table, DynamoDB, DuckDB, and Amazon DocumentDB — one implementation, one test suite, a choice of database at deployment time.

**SQLite is deliberately included.** Our native `GoLive.Saturn.Data.Sqlite` provider is **not yet released**, so this adapter carries SQLite too — both as the interim shipping option for SQLite users and as the fastest server-free CI target. MongoDB and LiteDB remain with their existing native providers and are out of scope here.

The dominant engineering problem is **impedance mismatch**, not query translation. Shiny.DocumentDb is designed for *AOT, explicitly-declared document models*, while Saturn.Data is a *reflection-driven, discover-types-at-runtime* framework. Three consequences drive most of the design:

1. **No per-type model declaration at runtime.** Shiny.DocumentDb's per-type configuration (`cfg.Table`, `MapVersionProperty`, `AddSoftDelete`, `MapUniqueIndex`, spatial/vector/full-text) must be declared **before the store is built**. Saturn.Data discovers entity types lazily via `GetCollectionNameForType`. The provider must therefore implement our cross-cutting semantics (soft delete, optimistic version, collection naming) as **predicate + field writes** rather than relying on Shiny.DocumentDb's per-type mappings, and pre-register only *its own* internal types (which are statically known).
2. **This provider will be reflection-based, not AOT.** Every Shiny.DocumentDb API has an optional `JsonTypeInfo<T>` parameter defaulting to `null`, resolved from `JsonSerializerOptions.TypeInfoResolver` with reflection fallback (`UseReflectionFallback = true`, the default). A generic `TItem : Entity` repository cannot supply compile-time type info, so we run with reflection and must document that this provider forfeits AOT/trimming — with an opt-in type-info registry as the escape hatch.
3. **Capability gating is mandatory.** Backend features differ sharply (`Any`/array-unnest throws on MariaDB; transactions are real on relational + LiteDB but compensating elsewhere; `ExecuteUpdate`/`ExecuteDelete` are server-side on relational/Mongo/Cosmos but absent or client-side elsewhere; unique indexes and spatial/vector need per-type config). The provider needs a capability probe + fail-fast policy, and its test suite must be parameterised by backend.

---

## 2. What Shiny.DocumentDb actually is (verified)

| Aspect | Fact |
| --- | --- |
| Core package | `Shiny.DocumentDb` — contains `IDocumentStore`, `DocumentStore`, the fluent query builder, and the expression visitor/translator. DI (`AddDocumentStore`) and OpenTelemetry ship **in core** |
| Backend packages | One per provider: `.Sqlite`, `.Sqlite.SqlCipher`, `.MySql`, `.MariaDb`, `.SqlServer`, `.PostgreSql`, `.CockroachDb`, `.Oracle`, `.LiteDb`, `.CosmosDb`, `.MongoDb`, `.DocumentDb` (Amazon DocumentDB), `.Redis`, `.RavenDb`, `.Firestore`, `.AzureTable`, `.DynamoDb`, `.DuckDb`, `.IndexedDb` |
| Target framework | `net10.0` — matches this repo exactly |
| Entry points | `new DocumentStore(new DocumentStoreOptions { DatabaseProvider = new PostgreSqlDatabaseProvider(conn) })`, or `services.AddDocumentStore(opts => ...)`, or provider-specific `new MongoDbDocumentStore(...)` / `new LiteDbDocumentStore(...)` / `AddAzureTableDocumentStore(...)` / `AddDynamoDbDocumentStore(...)` |
| Storage model | One JSON body per document, plus envelope columns (`TypeName`, `Id`, timestamps). Default `TableName = "documents"` — **one table holds every type, discriminated by `TypeName`**. A per-type `cfg.Table` gives a dedicated table but is exclusive to one type |
| Identity | A public `Id` property (`Guid`/`int`/`long`/`string`, or a `MapIdType` custom type) is **mandatory**; auto-generated when default and written back to the object. Satisfied by `Entity.Id` (string) |
| Id type affinity | Int/Long auto-generation is unsupported on Azure Table and DynamoDB. Saturn always assigns the Id itself, so this is moot |
| Queries | Fluent `store.Query<T>().Where(λ).OrderBy(λ).Paginate(skip, take).ToList()`; First/FirstOrDefault/Single/SingleOrDefault (predicate **and** string overloads); `.Select()` server-side projections; GroupBy/Having with `Sql.Count/Sum/Avg/Min/Max`; `.PageResult(page,pageSize)`; keyset `.ToCursorPage(cursor,take)`/`.ToCursorStream(n)`; `.ToAsyncEnumerable()`; count/sum/average terminals; `WhereIn`/`WhereNotIn` |
| Raw SQL escape hatch | `store.Query<T>(whereClause, parameters)` / `QueryStream<T>` / `Count<T>` — provider-specific JSON functions. **Not supported on MongoDB, LiteDB, IndexedDB** |
| Inspect | `.ToQueryString()` returns SQL + parameters (relational/Cosmos) or BSON (Mongo); **LiteDB/IndexedDB throw** |
| Writes | `Insert`, `Update(doc, patch: bool)`, `Upsert(doc, patchIfUpdate: bool)`, `Remove<T>(id)` → `bool`, `Clear<T>()` → `int`, `SetProperty<T>(id, λ, value)`, `RemoveProperty<T>(id, λ)`, `ExecuteUpdate(b => b.Set(...))`, `ExecuteDelete`, `BatchInsert/BatchUpsert/BatchUpdate/BatchRemove`, `Get<T>(id)`, `GetDiff(id, modified)` → RFC 6902 `JsonPatchDocument<T>` |
| Merge semantics | `Update` = full replace; `Upsert` = RFC 7396 merge-or-insert; `Update(patch:true)` / `Upsert(patchIfUpdate:false)` non-default modes are **relational-only** (others throw `NotSupportedException`) |
| Transactions | `store.OpenSession()` → `IDocumentSession` (write buffer + `SaveChanges`); explicit `session.BeginTransaction()` (relational) with `IsolationLevel` and `session.Get(id, LockMode.Update)` pessimistic locking (not on SQLite/DuckDB). Capability: `store.SupportsTransactions` |
| Concurrency | `cfg.MapVersionProperty(x => x.Version)` → version in the JSON body, set to 1 on insert, checked + incremented on update, `ConcurrencyException` on conflict. Works on **every** provider |
| Soft delete | `cfg.AddSoftDelete(x => x.IsDeleted)` (bool or nullable `DateTime`/`DateTimeOffset`) → `IncludeDeleted()`/`OnlyDeleted()`/`Restore`/`PurgeDeleted`/`HardDelete`. Built from a query filter + cancelling interceptor — "on every provider" |
| Indexes | `store.CreateIndexAsync<T>(λ, JsonTypeInfo<T>)` — non-unique JSON expression index (up to 30× faster). Unique: `cfg.MapUniqueIndex(λ)` / `cfg.MapProperty(λ, p => p.Unique())` — **per-type config**. Never use `CreateIndexAsync` for uniqueness |
| Change observation | `IObservableDocumentStore.NotifyOnChange<T>()` (in-process, relational + LiteDB) and `IChangeFeedDocumentStore.SubscribeChanges<T>` (any-writer: PostgreSQL LISTEN/NOTIFY, SQL Server Change Tracking, Cosmos Change Feed, DynamoDB Streams) |
| Other capabilities | Field encryption (`MapProperty(…Encrypt)`), spatial/vector/full-text (per-type config), temporal history, blobs, outbox, multi-tenancy (shared-table `TenantId` or tenant-per-database), seeders, `IDocumentBackup` export/restore, `IDocumentMaintenance.ClearAll` |
| Capability probes | `store.SupportsTransactions`, `SupportsSpatial`, `SupportsVector`, `SupportsFullText`, `SupportsRawJson`, `SupportsPessimisticLocking`, `MaxBlobSize`; optional interfaces via `is` (`ITemporalDocumentStore`, `IObservableDocumentStore`, `IChangeFeedDocumentStore`, `IDocumentBackup`, `IDocumentMaintenance`) |
| Provider connection model | Server SQL (PostgreSQL/MySQL/SQL Server/Oracle) opens a connection per operation and pools. **SQLite and DuckDB use a long-lived shared connection + serialization** (`IDatabaseProvider.RequiresSingleConnection`) |
| Table init | Lazy `CREATE TABLE IF NOT EXISTS` once per table on first touch; `SkipTableInitialization` for read replicas/DDL-less accounts |

> **Caveat:** everything above is from the project's readme and its bundled `skills/shiny-documentdb` guide, not from a local build. §14 lists what to verify against the actual package before implementation.

---

## 3. Why bother, and what it overlaps

**Unlocks (new backends):** PostgreSQL, CockroachDB, SQL Server, MySQL, MariaDB, Oracle 23ai+, DuckDB, Cosmos DB, Redis Stack, RavenDB, Google Firestore, Azure Table Storage, Amazon DynamoDB, Amazon DocumentDB, IndexedDB (WASM), SQLCipher.

**Overlaps:** MongoDB and LiteDB have native providers and stay there. SQLite also has a native provider (`Saturn.Data.Sqlite`), but it is **not released**, so it is included here — this adapter is the interim SQLite option and the fastest server-free CI target. When the native SQLite package ships, users can switch; the adapter's SQLite path stays useful for CI regardless.

**Why a single adapter is the right shape:** the whole point of Shiny.DocumentDb is that the same API and the same LINQ expressions run on all of those backends. Building 15 Saturn providers is not viable; building one that delegates is. The cost is that backend differences surface as runtime capability differences rather than as separate provider choices — hence §9's capability matrix and §8's fail-fast options.

---

## 4. Scope — what the provider must implement

The full Saturn provider surface, unchanged from the other providers:

- `IReadonlyRepository`, `IRepository`
- `IScopedRepository`/`IScopedReadonlyRepository`, `ISecondScopedRepository`/readonly, `IWeakScopedRepository`/readonly, `IWeakSecondScopedRepository`/readonly, `ITransparentScopedRepository`/readonly
- `IDatabaseTransaction` via `CreateTransaction()`
- `IRepositoryIndexManager.EnsureIndexes`
- `ICascadeExecutor` + `DeleteCascade`/`HardDeleteCascade`
- `IChangeFeedSink` (outbox) + `ChangeFeedPoller` DI
- `IDataUpdateDefinition<TItem>` provider implementation
- `IDisposable`
- DI registration via `ServiceScan` (`AddSaturnDocumentDbRepositoryServices`)

Shared contract suites to satisfy: `BasicRepositoryContractTests`, `ScopedRepositoryContractTests`, `ComprehensiveScopedRepositoryContractTests`, `CascadeContractTests`, `ChangeFeedContractTests` + `ChangeFeedPollerTests`, and the new `ChangeSetPatchContractTests`.

---

## 5. API mapping (Saturn member → Shiny.DocumentDb)

| Saturn | Shiny.DocumentDb | Notes |
| --- | --- | --- |
| `Insert(entity)` | `store.Insert(entity)` | Id pre-assigned by us; never rely on auto-gen |
| `Insert(IEnumerable)` | `store.BatchInsert(list)` | single transaction, atomic |
| `Save(entity)`/`Upsert` | `store.Upsert(entity)` (merge-or-insert) or `BatchUpsert` | `wasCreated` from a pre-existence probe |
| `Update(entity)` | `store.Update(entity)` (full replace) | `affected == 0` → `FailedToUpdateException` |
| `Update(predicate, entity)` | `Query<T>().Where(pred).First()` → mutate → `Update` | read-modify-write; `ExecuteUpdate` for atomic multi-field set where supported |
| `Delete(id/filter/ids)` (soft) | `SetProperty(id, x => x.IsDeleted, true)` + `SetProperty(DeletedAt/DeletedBy)` | our own soft delete, not `AddSoftDelete`, to avoid per-type config |
| `HardDelete` | `ExecuteDelete` on `Where(...)`, else `First`+`Remove`, else `BatchRemove` | capability-gated |
| `Restore` | `SetProperty(IsDeleted,false)` (+ clear `DeletedAt`) | ours |
| `ById` | `store.Get<T>(id)` | string id |
| `ById(ids)` | `Query<T>().WhereIn(x => x.Id, ids).ToList()` | `WhereIn` verified |
| `All`/`Many`/`One` | `Query<T>().Where(λ).OrderBy(λ).Paginate(skip,take).ToList()/First()` | page → `skip=(page-1)*size` |
| `Count` | `Query<T>().Where(λ).Count()` or `store.Count<T>(clause, params)` | |
| `Random` | `Count()` then a random `Paginate(skip,1)` | no native sampling; `OrderBy(random)` has no documented equivalent |
| `continueFrom` | `Where(x => x.Id > token).OrderBy(x => x.Id)` | string Id ordering; also composes with paging |
| `SortOrder`/`IQueryable` | `.OrderBy(λ)`/`.OrderBy(string)` and `.ToAsyncEnumerable()` | in-memory `IQueryable` fallback like the SQLite provider |
| `Patch(jsonDocument)` | parse `$set`/`$unset`/`$inc` → `SetProperty`/`RemoveProperty`/read-modify-write, guarded by a version predicate | `$inc` and multi-field may need `ExecuteUpdate`; capability-gated |
| `JsonUpdate(id, version, json)` | JSON collection `store.Collection(typeof(T)).Update(jsonObject, patch:true)` | **relational-only**; otherwise typed `Upsert` |
| `Increment(field, delta)` | read-modify-write with CAS retry | no `$inc` equivalent |
| `CreateTransaction()` | `store.OpenSession()` / `session.BeginTransaction()` | `SupportsTransactions` gates it |
| `IDataUpdateDefinition<T>` | `ExecuteUpdate(b => b.Set(...))` or read-modify-write | |
| `EnsureIndexes` | `CreateIndexAsync<T>(λ, typeInfo)` | non-unique only; unique/sparse/TTL are unsupported or per-type config |
| Cascade executor | `ExecuteUpdate`/`ExecuteDelete` with a `ScopeId` predicate, else read-ids + batch | |
| Change feed sink | our outbox stored as a document type (internal) + counter with CAS | design in §6.10 |
| `Entity.Id` / `Entity.Version` | `Id` property (default name matches); `Version` mapped only if we opt into `MapVersionProperty` | |
| `Ref<T>`/`HashedString`/`EncryptedString` | custom `JsonConverter`s on the shared `JsonSerializerOptions` | must also be understood by the expression visitor — predicates should use `ScopeId` strings |
| `Changes`/`EnableChangeTracking`/`_shortId` | excluded via a combined `IJsonTypeInfoResolver` modifier | reuse the SQLite provider's approach |

---

## 6. Architectural mismatches and decisions

Each item states the conflict, the options, and a recommendation.

### 6.1 Per-type model declaration vs lazy type discovery — the central problem

**Conflict.** `DocumentStoreOptions.ConfigureDocument<T>(...)` (table, version property, soft delete, unique indexes, spatial/vector/full-text, interceptors) is evaluated **when the store is built**. Saturn's `RepositoryOptions.GetCollectionName(type)` is consulted lazily, per operation, for arbitrary types — and nothing prevents an entity type appearing on the thousandth request.

**Options.**

- **A (recommended): one shared table, `TypeName`-discriminated; no per-type config for core semantics.** Leave `cfg.Table` unset, keep `TableName = "documents"`, use the default `TypeNameResolution.ShortName` (which equals `type.Name`, i.e. our collection naming). Implement soft delete, optimistic version and collection separation **ourselves** via predicates and field writes. Works identically on Azure Table/DynamoDB/Cosmos/Redis, which are single-table by nature.
- **B: honour `GetCollectionName` as a real per-type table.** Requires the consumer to declare every entity type before the store is built (a registration surface or a source-generated registration). Gives real table-per-type on relational backends and cheaper scans, but adds a mandatory setup step no other provider needs — and `cfg.Table` is exclusive to one type, so it can't be used for anything exotic.
- **C: hybrid.** A as the default; an opt-in `ConfigureDocumentTypes(Action<IDocumentAwareOptions>)`/registration list for consumers who want per-type tables, version mapping, unique indexes or spatial/vector/full-text.

**Recommendation:** **C.** Default to A so the provider "just works" like the others; expose the registration hook for per-type features. Document that a registration step is required for unique indexes, `MapVersionProperty` CAS, and spatial/vector/full-text.

### 6.2 Reflection vs AOT — accept reflection, offer an escape hatch

**Conflict.** Shiny.DocumentDb advertises 100% AOT/trim support, achieved by threading `JsonTypeInfo<T>` everywhere. A `TItem : Entity` repository cannot produce `JsonTypeInfo<TItem>` at compile time.

**Design.** Construct `JsonSerializerOptions` with `TypeInfoResolver = JsonTypeInfoResolver.Combine(entityModifierResolver, consumerContextOrNull, new DefaultJsonTypeInfoResolver())` and leave `UseReflectionFallback = true`. Expose `DocumentDbRepositoryOptions.JsonSerializerContext`/`TypeInfoResolver` so AOT consumers can register their own contexts; when present, type info resolves from it first.

**Consequence to document loudly:** this provider is **not AOT/trim-safe** in its default configuration. That is a deliberate trade for the generic API and is the main reason to keep the native providers for mobile/AOT workloads. A future enhancement is to have `Saturn.Generator.Entities` emit a `JsonSerializerContext` per consumer assembly (the generator work already exists in this repo), closing the gap.

### 6.3 Identity — keep the 24-hex string; do not convert to `Guid` (decided)

Saturn assigns `Entity.Id` (24-char lowercase hex, normalised by `Entity.TryParseId`) before every write, so we never depend on Shiny.DocumentDb's generation or its IdKind rules. The `Id` property name matches the library's default, and `Get<T>`/`Remove<T>`/`SetProperty<T>` all accept `string`.

**Decision: the Id is persisted as the 24-hex string, unchanged, on every backend.**

The alternative considered was to reinterpret the 12-byte ObjectId as a 16-byte `Guid` (4 zero pads) and use the library's native `Guid` Id support. It is technically possible and can be made order-preserving — pad the 12 bytes and build the Guid with `new Guid(span, bigEndian: true)` so `ToString("N")` reproduces the hex digits in order (note that `new Guid(byte[])`/`ToByteArray()` use mixed-endian field layout and would scramble the ordering). It is nonetheless rejected:

1. **Id comparison order becomes backend-dependent.** Keyset paging is `Id > token`; the comparison runs in the database against the Id column. A string compares lexicographically everywhere. A `Guid`/`uuid`/`uniqueidentifier` does not: PostgreSQL `uuid` compares as 16 big-endian bytes, but **SQL Server `uniqueidentifier` uses its infamous byte-swapped ordering** which is *not* byte order, and MySQL's `BINARY(16)` versus `CHAR(36)` differ again, while MongoDB's C# driver `GuidRepresentation` modes can reorder bytes. That would make "give me everything after this Id" produce different results per backend — the exact capability divergence this provider is designed to avoid.
2. **Cross-provider identity divergence.** Mongo, LiteDbX, Stellar and Sqlite all persist the hex string. Storing a `Guid` here means the same logical entity has two different stored ids depending on provider, which breaks cross-provider backup/restore, migration, and any external system holding those ids.
3. **The gain is negligible and the risk is not.** 24 bytes versus 16 in one envelope column is immaterial, and on the backends where keyset paging matters most (Azure Table `RowKey`, DynamoDB `sk`) the Id is a **string sort key** regardless — a Guid would be stringified anyway, or would degrade the range query.
4. **It would destroy the ordering benefit of the generator.** The ObjectId-style generator puts a timestamp in the leading bytes, so hex-string order is chronological: good B-tree insert locality and a monotonic `Id > token` key. Storing as SQL Server `uniqueidentifier` would scramble exactly that.

**Keyset pagination design (confirmed).** `continueFrom` is implemented as a range predicate on the Id, with the sort pinned to the Id so no tiebreaker is needed (Ids are unique):

```
Query<T>().Where(x => x.Id > token).OrderBy(x => x.Id).Paginate(0, n).ToList()
```

The Id lives in the library's envelope column, so this is a range seek on relational backends and a native range query on the key-partitioned stores, not a scan. Two follow-ups are verification items: the Id column's collation/type must yield hex byte-order on SQL Server and MySQL, and on Cosmos the Id field may need an index (registration, §6.1C) for the range to be served.

**Why not UUIDv7 instead?** v7 (RFC 9562) is time-ordered — a 48-bit big-endian Unix-ms prefix — so it *looks* like the answer to SQL Server's ordering. It isn't, because SQL Server compares `uniqueidentifier` in its byte-swapped order (last 6 bytes first), which ignores v7's timestamp prefix entirely. Getting chronological clustering on `uniqueidentifier` requires the `NEWSEQUENTIALID`-style byte reshuffle, i.e. a per-backend hack, which is the same divergence in a different costume. Making v7 work would need control over the Id column type (`binary(16)` on SQL Server, `uuid` on PostgreSQL, `BINARY(16)` on MySQL) and that is the library's choice, not ours.

The distinction that actually matters is **storage type, not id scheme**: a **string** column has one collation and therefore one ordering on every backend; a **Guid** column has a different ordering per backend. So if v7 semantics were ever wanted, the correct move is to store a v7 value **as a string** (hex-encoded or base64), which keeps ordering backend-independent while retaining the envelope-column/sort-key range seek.

Precision on what SQL Server's ordering breaks: keyset paging stays *correct* under any stable total order that is also used for the sort (there is an index, and the token is compared with the same collation), so no rows are lost or repeated. What is lost is (a) chronological locality, (b) the meaning of the token — "after id X" no longer means "newer than X" — and (c) agreement with our contract tests and paging expectations, which are written against ordinal hex ordering.

Adopting v7 would also be an entities-level breaking change, not a provider change: `Entity.TryParseId`, the id generator, and every stored ObjectId would move to a new value space, breaking ObjectId compatibility with the other four providers and requiring a migration. The only thing it buys over the current scheme is 8 bytes and the BCL's `Guid.CreateVersion7()` — while `UseGuidV7Ids` in the library only affects *auto-generated* ids, which we never use because we always assign the id ourselves.

### 6.4 Soft delete, version and shadow columns

We implement these ourselves, because they are the pieces that would otherwise force per-type configuration:

- **Soft delete:** write `IsDeleted`/`DeletedAt`/`DeletedBy` (via `SetProperty`, or read-modify-write for multiple fields), and AND a `x.IsDeleted == false` predicate into reads when `SupportsSoftDelete<TItem>()`. The predicate is built with `Expression.Property`/`Expression.Constant` at runtime, as the SQLite provider already does for `_deleted`.
- **Optimistic version:** `Patch`/`Increment` need `expectedVersion` semantics. Recommended default: `ExecuteUpdate` on `Query<T>().Where(x => x.Id == id && x.Version == expected)` with the affected-count check → `FailedToUpdateException`. Where `ExecuteUpdate` is unavailable, fall back to `MapVersionProperty` (requires registration) or a best-effort read-modify-write.
- **Shadow scope columns:** none. Shiny.DocumentDb stores the whole document; `ScopeId`/`SecondScopeId`/`Scopes` are ordinary serialised properties.

### 6.5 Transactions and read semantics

**Conflict.** Our shared contract test `TransactionTests.Read_Inside_Transaction_Sees_Uncommitted_Write` assumes a real transaction. Shiny.DocumentDb's `IDocumentSession` is documented as a *write buffer* — "reads don't see operations buffered in an uncommitted unit" — while explicit `session.BeginTransaction()` (relational only) is the closer analogue.

**Design.** `CreateTransaction()` opens a session and begins an explicit transaction where `store.SupportsTransactions`; on SQLite/DuckDB that is a whole-database lock, and on non-transactional backends (Cosmos, Azure Table, DynamoDB, Redis, RavenDB, Firestore) the provider reports `SupportsTransactions = false` and throws `NotSupportedException` — exactly how the Stellar provider handles it today, and the shared tests already guard on that capability.

**Verification item:** whether reads inside an explicit transaction observe prior writes in that transaction. If they don't, the affected contract test needs a provider-conditional expectation (the SQLite provider's transaction test is ours to adapt; the *shared* suite is capability-guarded, so we must confirm which side of the guard we land on).

### 6.6 Query translation and capability gating

Delegating predicates to Shiny.DocumentDb removes the need for a translator, but moves the risk to **backend differences**:

- MariaDB: no `JSON_TABLE` → `Any`/`All` over a collection, collection aggregates and array `GroupBy` throw `NotSupportedException`.
- LiteDB/IndexedDB: predicates evaluate in-process (no pushdown); `ToQueryString` throws.
- Azure Table/DynamoDB: `Query<T>()` is a single-partition scan; rich predicates evaluate client-side unless the property is a promoted column via `MapIndexedProperty` (per-type config); `Project` unsupported.
- Cosmos/LiteDB: `GroupBy` groups client-side; Azure Table/DynamoDB throw.
- Joins: relational + MongoDB only; unsupported elsewhere.

**Design.** Introduce `DocumentDbCapabilities` (probed from `IDocumentStore` + a small per-backend table we maintain) and a `StrictTranslation`-style option:

- `UnsupportedPredicateBehaviour = Throw | FallbackToClient` (default `Throw`), mirroring the SQLite provider's `StrictTranslation`.
- `FallbackToClient` materialises the collection and applies the compiled predicate in memory — correct but unbounded; log it.
- Documented per-backend notes for `Any`/`Scopes.Contains`, `GroupBy`, and `Project`.

Also: the provider must **use `ScopeId`/`SecondScopeId` (strings) for scope predicates**, not `Ref<T>` equality, because `Ref<T>` is an object in the expression tree to a JSON-oriented translator. `ScopedEntity.ScopeId` is already a public string property, so `x => x.ScopeId == scope` translates cleanly. Membership (`Scopes.Contains`) stays capability-gated.

### 6.7 Patch / JsonUpdate / Increment

Three mechanisms exist in Shiny.DocumentDb and map imperfectly onto Saturn's PATCH contract:

| Operation | Mapping | Limitation |
| --- | --- | --- |
| `$set` single field | `SetProperty<T>(id, λ, value)` | **no predicate** → no CAS on its own |
| `$set` many fields | `ExecuteUpdate(b => b.Set(...))` | server-side on relational, Mongo, Cosmos; capability-gated |
| `$unset` | `RemoveProperty<T>(id, λ)` | same CAS caveat |
| `$inc` | read-modify-write or `ExecuteUpdate` with an expression | no `$inc`; needs CAS retry |
| whole-document JSON | `store.Collection(typeof(T)).Update(jsonObject, patch: true)` | **relational-only**; unavailable inside a session |

**Recommendation.** Default `Patch` to `ExecuteUpdate` (atomic + version-predicated) with `FallbackToClient` for providers that lack it; keep `JsonUpdate` on the typed path for non-relational backends. Expose `PatchStrategy = ExecuteUpdate | ReadModifyWrite | JsonLane` in options.

### 6.8 Indexes

- `EnsureIndexes` → `CreateIndexAsync<T>(λ, typeInfo)` — runtime, non-unique, works per definition.
- `IndexOptions.Unique` → `cfg.MapUniqueIndex(λ)` — **per-type config**, so only workable when the consumer registers types (decision 6.1B/C). Otherwise report unsupported via `OnUnsupportedIndexOption`.
- `Sparse` → partial index exists only for unique mappings (`filter:`); report unsupported for plain indexes.
- `ExpireAfter` (TTL) → no equivalent; report unsupported, as the SQLite provider already does.

### 6.9 Cascade

Our `DeleteCascade`/`HardDeleteCascade` resolve children through reflection over `[CascadeDelete]`/`[CascadeDeleteOnScope]` and then apply set-based updates/deletes. Both halves map:

- Discovery: unchanged (provider-agnostic, already implemented in the SQLite provider).
- Apply: `Query<T>().Where(x => x.ScopeId == parentId).ExecuteUpdate(...)` / `.ExecuteDelete()` where supported; otherwise read ids + `BatchUpdate`/`BatchRemove`.

### 6.10 Change feed — the hardest piece

Our contract (`OutboxChangeFeedSink`) requires: an atomically-allocated **monotonic `long` sequence per source**, append with the sequence, and read `WHERE source = @s AND seq > @after ORDER BY seq LIMIT @take`.

Shiny.DocumentDb gives no atomic increment. Options:

- **A (recommended): internal counter document + CAS.** The counter type is **ours** (statically known), so it *can* be pre-registered with `cfg.MapVersionProperty` — we are not blocked by 6.1. Allocate `seq = current + 1` with a `ConcurrencyException` retry loop, then insert the outbox row. Wrap counter + row in one `IDocumentSession` when `SupportsTransactions`, else accept a compensating write with a documented at-least-once ordering caveat. Outbox rows are a Saturn-internal document type, so their shape is fixed.
- **B: use Shiny.DocumentDb's own `AddOutbox`/`IOutboxDispatcher`.** Rejected as the primary: its semantics (attempt counters, exponential backoff, dead-lettering, at-least-once delivery) don't match our `IChangeFeedSink` sequence/read-after contract without a shim over its storage.
- **C: native change-feed adapter.** For PostgreSQL, SQL Server, Cosmos and DynamoDB, implement `IChangeFeedSink` over `IChangeFeedDocumentStore.SubscribeChanges<T>` (real any-writer push). Excellent for those backends; not portable. Offer as an opt-in `ChangeFeedMode = Outbox | Native`.

Also note `IObservableDocumentStore.NotifyOnChange<T>` (in-process, relational + LiteDB) is *not* a substitute — it only sees this store instance's writes, whereas our outbox must feed a poller.

### 6.11 Random

No native sampling (Mongo's `$sample` has no documented equivalent here). Emulate: `count = Count(λ)`; if `count == 0` return empty; pick `skip = rng.Next(count)`; `Paginate(skip, count)`. Two round trips, and `Paginate` is client-side on some backends. Document the cost.

### 6.12 Serialization contract

- Reuse the SQLite provider's converters (`RefJsonConverter`, `WeakRefJsonConverter`, `PropertiesJsonConverter`) and the `EntityJsonTypeInfoResolver` modifier that drops `Changes`, `EnableChangeTracking`, `_shortId`.
- The modifier must be **combined** with any consumer-supplied `JsonSerializerContext` via `JsonTypeInfoResolver.Combine`, so both the serializer and the expression visitor see one configuration (the library explicitly requires sharing `options`).
- Enums: `JsonStringEnumConverter` for parity with the other providers.
- `HashedString`/`EncryptedString`: reuse `Saturn.Data.Entities.JsonConverters`. Note Shiny.DocumentDb also has its own field encryption (`MapProperty(p => p.Encrypt(...))`) — do **not** use it; it's per-type config and would diverge from the other providers. Keep our crypto.

### 6.13 Multi-tenancy

Shiny.DocumentDb has its own shared-table (`TenantId`) and tenant-per-database models. Saturn's scope mechanism is orthogonal and separate. **Decision: leave Shiny.DocumentDb tenancy off** and keep Saturn scopes (`ScopeId`/`ScopeId2`) as pure data. Mixing the two would double-filter and confuse `TransparentScopeProvider` semantics. Expose nothing of theirs in v1.

### 6.14 Disposal and single-connection providers

`IDocumentStore` is the DI-registered singleton and is disposable; our repository disposes it if it constructed it. For `RequiresSingleConnection` backends (SQLite, DuckDB) Shiny.DocumentDb already serialises writes internally — the provider must **not** add its own write gate (unlike the SQLite provider) and must not open a second store on the same file.

### 6.15 Unsupported-by-backend surface, summarised

Anything in §5 marked capability-gated must fail with a clear, actionable message naming the backend and the missing feature — never silently degrade. Proposed message shape: `NotSupportedException("Shiny.DocumentDb backend 'MariaDb' does not support collection membership predicates (Any/json_table). Use PostgreSql, or set UnsupportedPredicateBehaviour=FallbackToClient.")`

---

## 7. Proposed project and package layout

```
Saturn.Data.DocumentDb/
├─ Saturn.Data.DocumentDb/
│  ├─ GoLive.Saturn.Data.DocumentDb.csproj      (PackageId GoLive.Saturn.Data.DocumentDb)
│  ├─ DocumentDbRepository.cs                    ctor, store ownership, helpers
│  ├─ DocumentDbRepository.ReadonlyRepository.cs
│  ├─ DocumentDbRepository.Repository.cs
│  ├─ DocumentDbRepository.Scoped*.cs            (10 files, mirroring the SQLite provider)
│  ├─ DocumentDbRepository.Cascade.cs
│  ├─ DocumentDbRepository.Indexes.cs
│  ├─ DocumentDbRepositoryOptions.cs
│  ├─ DocumentDbCapabilities.cs                  probe + per-backend table
│  ├─ DocumentDbDataUpdateDefinition.cs
│  ├─ DocumentDbTransaction.cs                   IDocumentSession ↔ IDatabaseTransaction
│  ├─ Serialization/                             converters + combined resolver
│  ├─ Query/                                     predicate builder, capability gate, fallback
│  └─ ChangeFeed/                                outbox sink + DI extension (+ native adapter)
└─ Saturn.Data.DocumentDb.Tests/
   ├─ GoLive.Saturn.Data.DocumentDb.Tests.csproj
   ├─ ProviderFixtures/                          SQLite, LiteDb, DuckDb, and container-backed
   └─ ...contract subclasses + capability-specific tests
```

**References:** `Shiny.DocumentDb` (core) only. The **consumer** adds their backend package (`Shiny.DocumentDb.PostgreSql` etc.) and supplies an `IDatabaseProvider` or an already-built `IDocumentStore` — the same pattern as the Stellar provider taking a connection string and the Mongo provider taking a client. This keeps one Saturn package serving every backend.

---

## 8. Proposed options surface

```csharp
public sealed class DocumentDbRepositoryOptions
{
    // Exactly one of these is required
    public IDocumentStore Store { get; set; }
    public Action<DocumentStoreOptions> ConfigureStore { get; set; }

    public string DefaultTableName { get; set; } = "documents";
    public bool HonorCollectionNames { get; set; }                 // decision 6.1B/C
    public Action<DocumentDbTypeConfiguration> ConfigureTypes { get; set; }

    public UnsupportedPredicateBehaviour UnsupportedPredicateBehaviour { get; set; } = UnsupportedPredicateBehaviour.Throw;
    public PatchStrategy PatchStrategy { get; set; } = PatchStrategy.ExecuteUpdate;
    public ChangeFeedMode ChangeFeedMode { get; set; } = ChangeFeedMode.Outbox;

    public JsonSerializerContext JsonSerializerContext { get; set; }   // AOT escape hatch
    public IJsonTypeInfoResolver AdditionalTypeInfoResolver { get; set; }
    public bool UseReflectionFallback { get; set; } = true;

    public Action<string> OnUnsupportedIndexOption { get; set; }
    public Action<string> OnClientSideFallback { get; set; }

    public DocumentDbEntitySerializerOptions Serializer { get; set; } = new();
}
```

`DocumentDbCapabilities` reports `SupportsTransactions`, `SupportsExecuteUpdate`, `SupportsExecuteDelete`, `SupportsJsonLane`, `SupportsCollectionPredicates`, `SupportsClientSideGrouping`, `RequiresSingleConnection`, and the backend name — derived from the store's own probes plus a maintained table, so error messages and test skips are consistent.

---

## 9. Backend capability matrix (for our operations)

Legend: ✅ server-side · ⚠️ client-side/in-memory · ❌ throws/unsupported · `?` verify

| Saturn operation | SQLite / SQLCipher | DuckDB | PostgreSQL / Cockroach | SQL Server | MySQL / MariaDB | Oracle | LiteDB | Cosmos | Mongo / DocDB | Redis | RavenDB | Firestore | Azure Table | DynamoDB |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CRUD + `Get` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Predicates (`Where`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| OrderBy / Paginate | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | ⚠️ |
| `Count` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| `ExecuteUpdate`/`Delete` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ? | ✅ | ✅ | ? | ? | ? | ❌ | ❌ |
| JSON lane (`Collection`) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `SetProperty`/`RemoveProperty` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ? | ? | ? | ? | ? | ? | ? | ? |
| Transactions | ✅ (db lock) | ✅ (db lock) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ compensating | ⚠️ | ? | ? | ❌ | ❌ | ⚠️ |
| `MapVersionProperty` CAS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (ETag) | ✅ (cond. write) |
| Unique index | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Collection `Any`/`Scopes.Contains` | ✅ | ✅ | ✅ | ✅ | ❌ MariaDB | ✅ | ⚠️ | ✅ | ✅ | ? | ⚠️ | ? | ⚠️ | ⚠️ |
| GroupBy | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | ⚠️ | ⚠️ | ? | ⚠️ | ? | ❌ | ❌ |
| Raw change feed | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ? | ❌ | ? | ❌ | ✅ |
| Max doc size | — | — | — | — | — | — | — | — | — | — | — | — | ~64 KB | 400 KB |

This table must be **validated against the package** (see §14) and then maintained as the source of truth for the skip-guards in tests and for the capability checks at runtime.

---

## 10. Testing strategy

**Tier 1 — gates every build (no server):** **SQLite and DuckDB.** SQLite is included by decision (the native provider is unreleased) and is the fastest real signal: it exercises the full relational pipeline, lazy table init and every shared contract suite. DuckDB covers the second embedded `IDatabaseProvider` and the analytical path.

**Tier 2 — local/opt-in, not CI gates:** LiteDB (the client-side-evaluation path) and MongoDB (a sanity check of the adapter against our native provider's behaviour).
- Note: the library's SQLite backend deliberately pins `SQLitePCLRaw 2.1.x`, while our native `Saturn.Data.Sqlite` provider resolves `Microsoft.Data.Sqlite 10.0.12`. Both are 2.1.x so they should coexist; verify, and if they don't, keep the two providers' fixtures in separate test assemblies/processes.

**Tier 2 — container-backed (optional job, `compose.yaml`/testcontainers):** PostgreSQL, MySQL, MariaDB, SQL Server, CockroachDB, Oracle 23ai, RavenDB, Redis Stack, MongoDB (for the DocumentDb adapter path only), DynamoDB Local, Azurite (Azure Table).

**Tier 3 — cloud-only (skipped by default, run manually/nightly):** Cosmos DB, Firestore, real DynamoDB, IndexedDB (WASM harness).

**Per-provider fixtures.** Generalise the existing `UnitTestableXxxRepository` + `DatabaseFixture` pattern behind `IRepositoryTestFixture<T>` parameterised by backend. Capability-gated suites (`SupportsTransactions`, JSON lane, unique indexes, collection predicates) use a per-backend trait so a backend skips what it can't do rather than failing.

**Specific tests beyond the shared suites:** capability probe correctness; error messages for gated operations; `Ref`/`HashedString`/`EncryptedString` round-trip; `Changes`/`_shortId` exclusion from stored JSON; `Patch`/`Increment` CAS on `ExecuteUpdate` backends; outbox sequence monotonicity under concurrency; cascade via `ExecuteDelete`; index creation verification; and a documented-unsupported test for TTL/sparse/unique-without-registration.

---

## 11. Risks and mitigations

| # | Risk | Impact | Mitigation |
| --- | --- | --- | --- |
| R1 | `ConfigureDocument<T>` must precede store construction → per-type config unusable for lazily-discovered types | Unique indexes, `MapVersionProperty` CAS, spatial/vector unavailable by default | Default to predicate-based semantics; explicit registration surface for the rest; capability-gated errors |
| R2 | Reflection mode forfeits AOT/trimming | Contradicts the library's headline feature | Document loudly; opt-in `JsonSerializerContext`; generator-emitted contexts later |
| R3 | Backend feature divergence surfaces at runtime | Same code works on PostgreSQL, throws on MariaDB | Capability probe + `Throw` default + per-backend docs + skip-guarded tests |
| R4 | Transaction semantics differ (write buffer vs real tx; compensating on Cosmos/NoSQL) | Our transaction contract tests can't pass everywhere | Report `SupportsTransactions=false`; throw like Stellar; capability-guard tests |
| R5 | Change-feed sequence needs atomic increment that the library lacks | Ordering/duplication bugs | Internal counter type pre-registered with `MapVersionProperty` + retry; session-wrapped when transactional; native adapter for Postgres/SQL Server/Cosmos/DynamoDB |
| R6 | `Ref<T>` equality is not translatable; `Scopes.Contains` unsupported on MariaDB | Scoped queries break or throw | Use `ScopeId` string predicates; gate membership predicates |
| R7 | Client-side evaluation on LiteDB/Azure Table/DynamoDB is a full type scan | Silent performance cliff at scale | `OnClientSideFallback` telemetry hook; docs; recommend `MapIndexedProperty` via registration for hot paths |
| R8 | SQLitePCLRaw version coexistence with `Saturn.Data.Sqlite` | Load failures when both providers are referenced | Verify early; isolate test fixtures if needed |
| R9 | Immature/young project (small star count, v14, weekly churn) and preview-era docs | API drift between releases | Pin the package version; keep the adapter thin and behind our abstraction; verification checklist before coding |
| R10 | Duplicate capability with the native Mongo/LiteDB providers (and SQLite once ours ships) confuses consumers | Wrong provider chosen | README guidance: DocumentDb is the choice for server/cloud backends and the interim SQLite option; move SQLite to the native provider when it ships |
| R11 | Azure Table ~64 KB / DynamoDB 400 KB item limits | Large documents fail at runtime | Surface `MaxBlobSize`-style limits; document per-backend caps |
| R12 | Provider-per-operation connection model vs SQLite/DuckDB single connection | Concurrency surprises | Do not add an external write gate; rely on the library's serialization; one store instance per database |

---

## 12. Non-goals (v1)

- Replacing or superseding the MongoDb or LiteDbX providers for their backends. SQLite is included here only because our native SQLite package is unreleased; when it ships, the native provider is the long-term choice there.
- Spatial/vector/full-text/temporal/blob mapping (they require per-type config and are outside Saturn's entity model).
- Shiny.DocumentDb multi-tenancy, seeders, outbox, Orleans, Aspire, OData/AI/MCP integrations.
- AOT support (documented limitation, opt-in escape hatch only).
- IndexedDB/WASM and SQLCipher backends in the initial test matrix (they should work; not prioritised).
- Schema evolution/migrations — the library is schema-free, and so are we.

---

## 13. Resolved decisions

| # | Question | Decision |
| --- | --- | --- |
| 1 | Package name | **`GoLive.Saturn.Data.DocumentDb`** |
| 2 | Default CI matrix | **SQLite + LiteDB + DuckDB** is acceptable; no container requirement for the default matrix |
| 3 | Per-type registration (6.1) | **Accepted** — the registration surface exists for unique indexes / CAS / per-type tables; the default remains predicate-only so the provider works without it |
| 4 | Change feed (6.10) | **Outbox with CAS**, consistent with the other providers. Native change-feed adapters are not the default |
| 5 | Unsupported-predicate behaviour (6.6) | **`Throw`** |
| 6 | `Patch` default (6.7) | **Atomic** — `ExecuteUpdate` where supported, capability-gated |
| 7 | Tenancy (6.13) | **Do not touch Shiny's tenancy.** Saturn's own scopes stay as they are; note Saturn supports only two scopes per entity today while a project may want many, so scope modelling is explicitly not delegated to the library |
| 8 | Duplicate backend coverage | **Include SQLite** (our native SQLite package is unreleased) as a CI gate; **exclude LiteDB and MongoDB**, whose native providers own them |

**Consequence of #8 (revised):** SQLite is back in as a first-class target, so there are two server-free gates — **SQLite** (fastest; full relational pipeline) and **DuckDB** (embedded analytical). LiteDB stays local-only and MongoDB is a sanity check against the native provider. The container-backed job (PostgreSQL and friends) remains optional and is the only coverage for the backends this provider actually exists for — that gap is the one to be conscious of in CI.

**Follow-up consequence of #7:** because Saturn's scope model is capped at two scopes per entity (`ScopeId`, `SecondScopeId`, plus `MultiscopedEntity.Scopes`) and the user's projects need more, the DocumentDb provider stores scope values as plain data and filters on `ScopeId`/`SecondScopeId`/`Scopes` exactly like the other providers — it must not lean on the library's `TenantId` column to model business scopes. Any move to a many-scope model is a Saturn-level change, not a provider change.

---

## 14. Verification checklist before implementation

Confirm these against the actual `Shiny.DocumentDb` package (I have only read its readme + skill guide):

1. `IDocumentStore` exact signatures for `Insert/Update/Upsert/Remove/Clear/Get/SetProperty/RemoveProperty/Count/Query/QueryStream`, and whether `Query<T>()` exposes `ExecuteUpdate`/`ExecuteDelete`.
2. Whether `ExecuteUpdate`/`ExecuteDelete` are available on each backend, and whether `Set` can compute from the existing value (needed for `$inc`).
3. Whether reads inside an explicit `session.BeginTransaction()` observe prior writes in that transaction (decides R4's test expectations).
4. `SupportsTransactions` and the other capability properties: exact names, types, and values per backend.
5. `CreateIndexAsync<T>` signature and whether it works without a supplied `JsonTypeInfo<T>` when `UseReflectionFallback = true`.
6. Whether custom `JsonConverter`s on `DocumentStoreOptions.JsonSerializerOptions` are honoured by the **expression visitor** as well as the serializer (critical for `Ref<T>`/`HashedString`/`EncryptedString`).
7. Whether `JsonTypeInfoResolver.Combine` is a supported configuration (our entity modifier + a consumer `JsonSerializerContext`).
8. Id handling: is a 24-hex string Id treated as opaque (no ObjectId parsing)? Does `Insert` write it back unchanged?
9. `MapVersionProperty` supported property types (`long?` vs `int`) if we use it for the outbox counter.
10. Package versions, TFMs, and transitive dependency conflicts — especially `SQLitePCLRaw` coexistence with `Microsoft.Data.Sqlite 10.0.12` (R8).
11. The exact table/`TypeName` naming behaviour with `TypeNameResolution.ShortName` to confirm it matches `GetCollectionName(type) == type.Name`.
12. Which provider packages can be exercised without a server, and whether their `compose.yaml` covers the rest.

---

## 15. Recommendation

Proceed, with the design in §6 tightened by your answers to §13. The provider should be **thin**: delegate all storage and query translation to `Shiny.DocumentDb`, implement Saturn's cross-cutting semantics (soft delete, CAS, scope filtering, change feed) with predicates and field writes rather than per-type configuration, gate every backend-specific capability behind an explicit probe with actionable errors, and treat reflection-based (non-AOT) operation as a documented, deliberate trade. The value is real — a dozen server and cloud backends behind one already-tested provider contract — and the risks are concentrated in capability divergence and change-feed sequencing, both of which have concrete mitigations above.
