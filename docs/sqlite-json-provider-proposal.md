# Proposal: SQLite + JSON as a Document Database Provider

**Status:** Draft for review
**Author:** Axiom
**Date:** 2026-09-23
**Scope:** Add a fourth backend provider to `Saturn.Data` that stores entities as JSON documents inside SQLite using the built-in JSON1 / JSONB extensions, implementing the same abstraction surface as `Saturn.Data.MongoDb` and `Saturn.Data.LiteDbX`.

---

## 1. Executive summary

Saturn.Data is a schema-less entity/document framework. Entities are CLR objects derived from `Entity`, collected into logical `GetCollectionNameForType` "collections", and surfaced through one large repository contract (`IReadonlyRepository`, `IRepository`) plus scoped/weak/transparent variants, cascade, and change feed.

The proposal is a new provider (`GoLive.Saturn.Data.Sqlite`) that:

- Stores each logical collection in its own SQLite table (`table-per-collection`).
- Stores the entity payload as a single JSON document column (`_doc`) — **no fixed per-property columns**.
- Uses SQLite's JSON1 extension (`json_extract`, `json_set`, `json_patch`, `json_each`, `json_object`, `->`, `->>`) and JSONB for all document reads/writes and predicate translation.
- Adds a small set of **provider-managed shadow columns** (`_id`, `_v`, `_deleted`, `_scope`, `_scope2`, `_archived`) that mirror the framework's cross-cutting concerns (identity, optimistic version, soft delete, scope) so indexing and filtering are fast without introducing per-entity schema.
- Implements real transactions, an outbox change feed sink, a cascade executor, and expression-driven indexes.
- Ships with the full shared contract test suite so behavioral parity with Mongo and LiteDbX is enforced.

This mirrors the LiteDbX approach (single server-free file, BSON mapper, outbox collections) while borrowing Mongo's maturity around scope normalization, soft delete, projection, and index creation.

---

## 2. Problem statement

The three existing providers cover:

| Provider | Backend | Storage | Transactions |
| --- | --- | --- | --- |
| `GoLive.Saturn.Data.MongoDb` | MongoDB server | BSON documents in collections | yes (sessions) |
| `GoLive.Saturn.Data.LiteDb` (LiteDbX) | embedded file | BSON documents, `BsonMapper` | yes |
| `GoLive.Saturn.Data.Stellar` | embedded FastDB | MessagePack blobs keyed by `EntityId` | no |

Missing: a **relational, serverless, SQL-queryable, single-file** provider. SQLite is the obvious gap — ubiquitous, zero-install, ACID, and (since 3.38, and with `Microsoft.Data.Sqlite`) JSON1 is compiled in by default. This lets Saturn.Data target desktop/mobile/edge/AOT-friendly deployments and enables interoperability with conventional SQL tooling, all while preserving the document model.

Requirements extracted from the existing contract surface:

1. **No fixed columns / document-first.** Entities are arbitrary `Entity` subclasses; the schema cannot be generated from a fixed model. Storage must accept any shape.
2. **Identity.** `Entity.Id` is a validated, normalized 24-char lower-hex ObjectId string (`Entity.TryParseId`). It must be the primary key and preserve ObjectId byte ordering (needed for continuation paging).
3. **Versioning.** `Entity.Version` (`long?`) participates in optimistic concurrency (`Patch`/`Increment` `expectedVersion`) and is bumped on every mutating write (`_v` in Mongo/LiteDbX).
4. **Soft delete.** `ISoftDeletable.IsDeleted/DeletedAt/DeletedBy`; `Delete` is logical, `HardDelete` physical, `Restore` clears. `ById`/`All`/`Many`/`One`/`Count`/`Random` accept `includeDeleted`.
5. **Scope.** `ScopedEntity<T>`, `MultiscopedEntity<T>`, `SecondScopedEntity<,>`, plus weak (`WeakRef`) variants. Scope is a `Ref<T>`/`WeakRef`/`WeakRef<T>` serialized as an id string, and `Scopes` is a `List<string>`. Queries filter by scope on every scoped call.
6. **Refs.** `Ref<T>.Id == "…"` and `WeakRef == "…"` must translate in expressions.
7. **Projections.** `All/ById/Many/One<TItem,TProjection>(selector)`.
8. **Pagination + continuation.** `pageSize`, 1-based `pageNumber`, and `continueFrom` token (stable only with `Id` ascending — same rule as Mongo/LiteDbX).
9. **Sorting.** Multi-key `SortOrder<TItem>` with ASC/DESC, stable `_id` tiebreak.
10. **Patch/Increment/JsonUpdate.** `Patch(id, expectedVersion, jsonDocument, updateDefinition)`, `Increment(id, field, delta, expectedVersion)`, including `$set` document semantics (see `ChangeFeedContractTests.Patch_Bumps_Version_Before_AfterPatch`).
11. **Write behaviors + change feed.** `After*` hooks dispatched by `BehaviorDispatcher`; a `ChangeFeedBehavior` appends an outbox row that must enlist in the caller's transaction and **must not** fail the repository operation if the sink throws.
12. **Cascade.** `ICascadeExecutor` resolving children by scope id.
13. **Indexes.** `IRepositoryIndexManager.EnsureIndexes`.
14. **DI.** `ServiceScan`-generated `*Repository` registration.

---

## 3. Prior art: what to copy and what to avoid

### 3.1 MongoDB provider (`Saturn.Data.MongoDb`)

Strengths to replicate:

- `GetCollectionNameForType` + `typeNameCache` (`MongoDbRepository.cs:196-204`).
- `BuildReadContext` / `BuildWriteContext` and the `BehaviorDispatcher` pipeline (`MongoDbRepository.cs:206-322`).
- Ref normalization in predicates (`RefExpressionRewriter.NormalizeForRef`, `ExpressionRewriters/RefExpressionRewriter.cs`).
- Soft-delete filters as set-based operations (`ApplySoftDeleteFilter` + `Update.Inc("_v",1)`).
- `CanApplyContinuation` guard and `_id`-secondary sort tiebreak (`MongoDbRepository.cs:541-640`).
- Server-side projection via `.Project(selector)` (`ReadonlyRepository.cs:279-349`).
- `EnsureIndexes` + `buildCreateIndexModel` (`MongoDbRepository.cs:78-140`).
- `ExecuteWithTransaction` helper centralizing session vs non-session paths (`MongoDbRepository.cs:643-675`).
- `Patch` uses `UpdateDefinition` + `Inc("_v",1)`; `Increment` uses `Update.Inc(field, delta)`.
- `Random` via `$sample`; cursor-based `IAsyncEnumerable`.

Things that do not translate to SQLite:

- BSON `ObjectId` on the wire, `BsonIgnoreIfDefault`, server-side `$sample`, change streams (`Watch`), `$expireAfterSeconds` TTL.

### 3.2 LiteDbX provider (`Saturn.Data.LiteDbX`)

Strengths to replicate:

- Single-file, embedded, no server; `LiteDatabase.Open(connectionString, mapper)` (`LiteDbRepository.cs:18-25`).
- Custom serialization for `Ref<>`, `HashedString`, `EncryptedString` (`EntityMapper.cs`).
- `BuildQuery` + `Query.GT("_id", token)` continuation (`LiteDbRepository.cs:131-170`).
- `TryNormalizeContinuationToken` / `NormalizeEntityIds` ObjectId handling.
- Query DSL presence (JSON-like query building) — analogous to SQLite JSON1.
- Outbox change feed via dedicated collections and a counters collection (`LiteDbOutboxChangeFeedSink.cs`).

Things to avoid / improve:

- `DeleteCore`/`RestoreCore` load-then-update one entity at a time; the SQLite provider should be set-based SQL.
- `Patch` is a client-side read-modify-write JSON merge; SQLite can do `json_set`/`json_patch` server-side.
- `LiteDbCascadeExecutor` reflects a `CollectionFor` method that does not exist on `LiteDbRepository` — the cascade path is effectively broken. Do **not** copy; expose a proper internal collection-name resolver.
- `Save`/`Upsert` enumeration paths call `Upsert` per item without the transaction; not ideal.
- LiteDbX `Insert`/`Save` ignore `ResolveLiteTransaction` in several paths.

### 3.3 Stellar provider (`Saturn.Data.Stellar`)

Useful patterns:

- `EntityId` value type for key ordering.
- `BuildWriteContext`/`BuildWriteResult` and behavior dispatch are shared scaffolding worth mirroring verbatim.
- `Patch`/`Increment` client-side fallback is a valid **Phase-1** correctness baseline (`StellarRepository.Repository.cs:493-644`).
- `CreateTransaction()` throws `NotImplementedException` and the change feed contract test guards on `SupportsTransactions`; SQLite **should** support transactions (that is a differentiator), so `SupportsTransactions = true`.

---

## 4. Design goals and non-goals

### Goals

- **G1** Document fidelity: any `Entity` graph round-trips (including `Ref`, `WeakRef`, `HashedString`, `EncryptedString`, enums, nested objects, `Properties`).
- **G2** Behavioral parity: pass all shared contract tests unmodified.
- **G3** Set-based SQL for predicates, soft delete, hard delete, restore, patch, increment, count.
- **G4** Real ACID transactions and concurrent reader support (WAL).
- **G5** Expression-based secondary indexes over JSON paths.
- **G6** Embedded, single-file, no external service; usable from net10.0 with `Microsoft.Data.Sqlite`.
- **G7** Full provider surface: unscoped, scoped (5), weak scoped (2), transparent scoped, second scope, cascade, change feed, index manager, DI.

### Non-goals (initially)

- Distributed/sharded storage; multi-writer server semantics.
- Cross-collection joins in a single LINQ query.
- TTL/expiry indexes (`IndexOptions.ExpireAfter`) — SQLite has no equivalent; documented as unsupported.
- Server-side aggregation pipeline; complex LINQ is handled by fallback (see §8).
- Encryption-at-rest via SQLCipher (Microsoft.Data.Sqlite does not bundle it). Could be a pluggable core provider later.
- Change streams / reactive push. Change feed is poll-based (same as LiteDbX).

---

## 5. Architecture overview

```
                     GoLive.Saturn.Data.Abstractions
        IReadonlyRepository / IRepository / IScoped* / ICascadeExecutor
        IChangeFeedSink / OutboxChangeFeedSink / IRepositoryIndexManager
                                   ▲
                                   │ implements
        ┌──────────────────────────┴─────────────────────────────┐
        │                Saturn.Data.Sqlite                      │
        │                                                        │
        │  SqliteRepository            (partial, all interfaces) │
        │   ├─ .cs                     ctor, connection, helpers │
        │   ├─ .ReadonlyRepository.cs  All/ById/Many/One/Count/… │
        │   ├─ .Repository.cs          Insert/Save/Update/…      │
        │   ├─ .Scoped*.cs             IScopedRepository family  │
        │   ├─ .Cascade.cs             DeleteCascade/HardDelete… │
        │  SqliteRepositoryOptions                               │
        │  SqliteEntityStore           DDL + row (de)serialize   │
        │  Serialization/                                        │
        │   ├─ EntityJsonSerializer    STJ options + converters  │
        │   ├─ RefJsonConverter, WeakRefJsonConverter            │
        │   ├─ EntityJsonTypeInfoResolver (ignore derived/CT)    │
        │   └─ PropertiesJsonConverter                          │
        │  Query/                                                │
        │   ├─ SqliteExpressionTranslator  Expr -> SQL + params  │
        │   ├─ SqliteJsonPathResolver      member -> $.path      │
        │   ├─ SqliteQueryable<T>, SqliteQueryProvider          │
        │   ├─ SqlPredicate / SqlFragment / SqlParam             │
        │   └─ SqliteQueryBuilder          WHERE/ORDER/LIMIT    │
        │  Indexes/SqliteIndexManager                            │
        │  Transactions/SqliteTransactionWrapper                 │
        │  Cascade/SqliteCascadeExecutor                         │
        │  ChangeFeed/SqliteOutboxChangeFeedSink + DI ext        │
        │  ServicesExtensions (ServiceScan partial)              │
        └────────────────────────────────────────────────────────┘
```

---

## 6. Storage model

### 6.1 Table-per-collection, document column

One table per collection name, created lazily on first use, guarded by a `ConcurrentDictionary<string, byte>` of known tables:

```sql
CREATE TABLE IF NOT EXISTS "<collection>" (
    _id        TEXT    NOT NULL PRIMARY KEY,   -- 24-char hex ObjectId
    _v         INTEGER NULL,                   -- Entity.Version
    _deleted   INTEGER NOT NULL DEFAULT 0,     -- denormalized ISoftDeletable.IsDeleted
    _scope     TEXT    NULL,                   -- $.Scope (Ref/WeakRef id)
    _scope2    TEXT    NULL,                   -- $.SecondScope (second scoped id)
    _archived  INTEGER NOT NULL DEFAULT 0,     -- IArchivable.IsArchived
    _doc       TEXT    NOT NULL               -- canonical JSON (optionally JSONB BLOB)
);

CREATE INDEX IF NOT EXISTS "ix_<collection>__scope"   ON "<collection>"(_scope);
CREATE INDEX IF NOT EXISTS "ix_<collection>__deleted" ON "<collection>"(_deleted);
CREATE INDEX IF NOT EXISTS "ix_<collection>__scope2"  ON "<collection>"(_scope2);
```

Rationale for shadow columns:

- `_id` is required for PK + ObjectId-ordered continuation.
- `_v` is required for optimistic concurrency and is set by every write.
- `_deleted` avoids evaluating `json_extract` on every soft-delete-aware read and allows a cheap partial index.
- `_scope`/`_scope2` are the framework's single hottest predicate (every scoped read) and allow a plain B-tree scope index rather than an expression index over JSON.
- `_archived` for `IArchivable` reads.

**These columns are not a per-entity schema.** They are framework-wide projection columns derived from well-known members. Arbitrary entity properties remain only inside `_doc`. New framework concepts can be added as columns later via `ALTER TABLE … ADD COLUMN` without breaking existing rows.

### 6.2 JSONB option

`Microsoft.Data.Sqlite` bundles a recent SQLite where JSONB is available (3.45+). `SqliteRepositoryOptions.UseJsonB` switches the column to `BLOB` and wraps writes in `jsonb(@doc)` / reads with `json(_doc)`. Default `false` (TEXT) for tooling-friendliness and debuggability; JSONB can be enabled for compactness/speed.

### 6.3 Schema tracking

A `__saturn_schema` table records created collections and shadow-column presence:

```sql
CREATE TABLE IF NOT EXISTS "__saturn_schema" (
    collection TEXT PRIMARY KEY,
    created_utc TEXT NOT NULL,
    schema_version INTEGER NOT NULL
);
```

DDL is idempotent (`IF NOT EXISTS`); `schema_version` enables future migrations (e.g., adding `_archived` to pre-existing tables).

### 6.4 SQL write/read templates

Shadow columns are populated from the JSON in the same statement, so no C#-side extraction is needed:

```sql
INSERT INTO T (_id, _v, _deleted, _scope, _scope2, _archived, _doc)
VALUES (
    @id,
    json_extract(@doc, '$.Version'),
    COALESCE(json_extract(@doc, '$.IsDeleted'), 0),
    json_extract(@doc, '$.Scope'),
    json_extract(@doc, '$.SecondScope'),
    COALESCE(json_extract(@doc, '$.IsArchived'), 0),
    @doc);
```

Upsert:

```sql
INSERT INTO T (_id,_v,_deleted,_scope,_scope2,_archived,_doc)
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
```

Soft delete (set-based):

```sql
UPDATE T SET
    _doc = json_set(_doc,
                    '$.IsDeleted', json('true'),
                    '$.DeletedAt', @now,
                    '$.DeletedBy', ''),
    _v = COALESCE(_v,0) + 1,
    _deleted = 1
WHERE <translated predicate>;
```

Hard delete: `DELETE FROM T WHERE <predicate>;`

Restore:

```sql
UPDATE T SET
    _doc = json_set(_doc, '$.IsDeleted', json('false'), '$.DeletedAt', json('null'), '$.DeletedBy', ''),
    _v = COALESCE(_v,0) + 1,
    _deleted = 0
WHERE <predicate>;
```

Increment (atomic server-side):

```sql
UPDATE T SET
    _doc = json_set(_doc, '$.<Path>', json_extract(_doc, '$.<Path>') + @delta),
    _v = COALESCE(_v,0) + 1
WHERE _id = @id [AND _v = @expectedVersion];
```

Patch (`{"$set":{...}}`, plus `$inc`/`$unset` later):

```sql
UPDATE T SET
    _doc = json_set(json_set(_doc, '$.Count', json(@p0)), '$.Name', json(@p1)),
    _v = COALESCE(_v,0) + 1
WHERE _id = @id [AND _v = @expectedVersion];
```

If no update operators are present, treat the top-level keys as a `$set` (LiteDbX-compatible merge), and support `json_patch(_doc, @merge)` for a straight RFC 7396 merge when `SqliteRepositoryOptions.UseJsonMergePatch` is enabled.

---

## 7. Serialization / JSON document contract

Route all document IO through `EntityJsonSerializer` built on `System.Text.Json` (`System.Text.Json` 10.0.12 is already referenced by `GoLive.Saturn.Data.Entities.JsonConverters`).

### 7.1 Converter set

| Concern | Converter | Behavior |
| --- | --- | --- |
| `Ref<T>` | `RefJsonConverter<T>` | Write `Id` as a JSON **string** (null when absent); read `new Ref<T>(value)`. Keeps `$.Scope` a scalar and makes JSON1 path queries trivial. |
| `WeakRef` / `WeakRef<T>` | `WeakRefJsonConverter` | Same string encoding. |
| `HashedString` | reuse `Saturn.Data.Entities.JsonConverters.HashedStringJsonConverter` | Existing package. **Note:** the shipped converter only persists `Populated`, not `Hash`/`Salt`; see §22 risk R7. Provider will supply a full-fidelity converter if needed. |
| `EncryptedString` | reuse `Saturn.Data.Entities.JsonConverters.EncryptedStringJsonConverter` | Same caveat as above. |
| Enums | `JsonStringEnumConverter` | Readable storage, stable queries. Mongo uses `AlwaysSerializeEnumsConvention`. |
| `Dictionary<string,object> Properties` | `PropertiesJsonConverter` | Write via `JsonSerializer.SerializeToElement(value)`; read as `JsonElement` (then scalar-unwrapped to string/bool/number where possible). |
| `decimal` | default number | Round-trips as JSON number; optional string mode via option for exactness. |
| `DateTime`/`DateTime?` | ISO-8601 round-trip ("O") | Lexicographic comparisons remain chronological for UTC values. |

### 7.2 Members excluded from persistence

`Entity` has derived/transient members that must not be round-tripped: `EnableChangeTracking`, `Changes`, `_shortId`. `ScopedEntity.ScopeId` and `SecondScopedEntity.SecondScopeId` are derived from the ref and should be excluded to avoid redundant/possibly inconsistent state.

Implement with a `DefaultJsonTypeInfoResolver` modifier (`TypeInfoResolver` on `JsonSerializerOptions`) that:

- Ignores `EnableChangeTracking`, `Changes`, `_shortId`.
- Ignores `ScopeId`/`SecondScopeId` (their values are recoverable from `Scope`).
- Configures no-naming-policy (PascalCase preserved) so JSON paths match C# names (`$.Scope`, `$.Name`, `$.IsDeleted`).

If excluding `ScopeId` proves incompatible with a shared test or an entity that only exposes `ScopeId` (no `Scope`), fall back to serializing both consistently.

### 7.3 Internal vs document key map

| Framework concept | Column | Document key |
| --- | --- | --- |
| Id | `_id` | `$.Id` (kept in sync) |
| Version | `_v` | `$.Version` |
| Deleted | `_deleted` | `$.IsDeleted` |
| DeletedAt/By | — | `$.DeletedAt`, `$.DeletedBy` |
| Scope | `_scope` | `$.Scope` |
| Second scope | `_scope2` | `$.SecondScope` |
| Archived | `_archived` | `$.IsArchived` |
| Any entity property | — | `$.<PropertyName>` (nested as needed) |

`Id` is stored both in `_id` and in `_doc` for full round-trip; writes always set both from the same value.

### 7.4 Polymorphism

- Table-per-concrete-type via `GetCollectionNameForType` (default `type.Name`), so no discriminator is needed for the common case.
- `Properties` values that are `object` lose CLR type on read (become `JsonElement`). Documented; users needing fidelity should use typed properties.
- Interface/abstract-typed properties: use an optional `$type` envelope driven by `SqliteRepositoryOptions.TypeDiscriminatorResolver`, mirroring Mongo's discriminators. Phase 7.

---

## 8. Query translation subsystem

This is the highest-risk area and the main differentiator from Mongo/LiteDbX. Unlike BSON mappers, SQLite needs an explicit expression→SQL translator.

### 8.1 JSON path resolution (`SqliteJsonPathResolver`)

Resolve a `MemberExpression` chain to a JSON path against `_doc`:

- `x.Id` → `_id` (column), unless nested.
- `x.Scope` / `x.ScopeId` → `$.Scope`.
- `x.SecondScope` / `x.SecondScopeId` → `$.SecondScope`.
- `x.Scopes` → `$.Scopes` (array).
- `x.Version` → `_v`.
- `x.<P>` → `$.<P>`; `x.<P>.<Q>` → `$.<P>.<Q>`.
- `Ref<T>.Id` access on a ref member → the ref path (`$.Scope`).
- Unwrap `Convert` nodes used for `object` boxing and for implicit `Ref<T>`/`WeakRef` conversions.

### 8.2 Supported nodes (server-pushdown v1)

| C# | SQL |
| --- | --- |
| `a == b`, `!=`, `>`, `>=`, `<`, `<=` | `json_extract(_doc,'$.path') <op> @p` |
| `&&`, `\|\|`, `!` | `AND`, `OR`, `NOT` |
| `"".Contains(s)` / `x.P.Contains(s)` | `instr(json_extract(_doc,'$.P'), @p) > 0` |
| `x.P.StartsWith(s)` | `json_extract(_doc,'$.P') LIKE @p || '%'` (escape `%`/`_`) |
| `x.P.EndsWith(s)` | `… LIKE '%' || @p` |
| `list.Contains(x.P)` | `json_extract(_doc,'$.P') IN (SELECT value FROM json_each(@pjson))` |
| `x.Scopes.Contains(s)` | `EXISTS (SELECT 1 FROM json_each(_doc,'$.Scopes') WHERE value = @p)` |
| `x.P == null` | `json_extract(_doc,'$.P') IS NULL` |
| bool expressions | normalize JSON bool → `0`/`1` constant |
| `Ref<T>.Id == s`, `Ref<T> == s`, `WeakRef == s` | `json_extract(_doc,'$.Ref') = @p` |
| `x.P.Version` / `x.P.Id` | nested path resolution |
| captured constants / closures | partial evaluation to parameters |

### 8.3 Fallback strategy

`SqliteExpressionTranslator` throws `SqliteTranslationException` for unsupported nodes. `SqliteQueryBuilder` catches it and:

- If `SqliteRepositoryOptions.StrictTranslation == true` → rethrow (fail fast, recommended for production).
- Else → mark the query as **in-memory** and delegate the predicate to `IEnumerable<T>.Where(compiled)` after materializing the candidate set.

When an in-memory predicate is combined with pagination/sorting, pagination/sorting must be applied **after** the in-memory filter to preserve correctness (documented performance cliff). Counting the same way.

This gives 100% correctness for arbitrary LINQ with a documented cost, and a supported fast path for the common DSL.

### 8.4 `IQueryable<TItem>`

`MongoDbRepository`/`LiteDbX` return provider-native `IQueryable` (LINQ to Mongo / LiteDB LINQ). SQLite has no such thing, so implement a minimal query provider:

- `SqliteQueryable<T>` + `SqliteQueryProvider : IQueryProvider`.
- Recognizes `Queryable.Where`, `OrderBy(Descending)`, `ThenBy(Descending)`, `Skip`, `Take`, `Select`, `First(OrDefault)`, `Single(OrDefault)`, `Count`, `Any`.
- Translates recognized shapes to SQL via `SqliteQueryBuilder`; anything else falls back to `AsEnumerable()` and LINQ-to-Objects so semantics are still correct (just not pushed down).
- Soft-delete filtering applied by default unless `IQueryable(includeDeleted:true)`.

### 8.5 Projections

Two modes:

1. **Default (correct):** materialize entities, apply `selector.Compile()`.
2. **Server-side (Phase 6 optimization):** when the selector is a simple member-init or anonymous projection over JSON paths, build `json_object('P', json_extract(_doc,'$.P'), …)` and deserialize into `TProjection`. Keeps `All/ById/Many/One<TItem,TProjection>` parity with Mongo's `.Project(selector)` while remaining optional.

### 8.6 Sorting, pagination, continuation

- **Sort:** `ORDER BY json_extract(_doc,'$.P') ASC|DESC, …`. When the primary sort is not `Id`, append `_id ASC` (Mongo parity, `getSortDefinition`).
- **Page:** 1-based `pageNumber` → `LIMIT @size OFFSET (pageNumber-1)*size`; else `LIMIT @size`.
- **ContinueFrom:** only when the first sort is `Id` ascending (`CanApplyContinuation` parity) → append `_id > @token`; otherwise ignore the token. Token is the normalized 24-hex ObjectId; TEXT ordering equals ObjectId byte ordering.

### 8.7 `whereClause` dictionary overloads

`Dictionary<string,object> whereClause` maps key → JSON path equality (`Many`/`One`). For `Scopes`/`Ref` keys, apply the same special-casing as predicates. This mirrors `LiteDbRepository.BuildWhereClausePredicate`.

### 8.8 Random

`SELECT … WHERE <predicate> ORDER BY RANDOM() LIMIT @count` (contrast Mongo `$sample`).

---

## 9. CRUD operation semantics

Each operation follows the Mongo/LiteDbX shape: build `RepositoryWriteContext`, dispatch before behaviors, execute, build `RepositoryWriteResult`, dispatch after behaviors, and call `ApplyOnWriteFailed` on exception.

| Operation | SQL strategy | Failure contract |
| --- | --- | --- |
| `Insert(T)` | `INSERT`; PK conflict → `SqliteException` surfaced | — |
| `Insert(IEnumerable)` | single `INSERT` per row inside one transaction; return all ids | ordered |
| `Save(T/IEnumerable)` | existence probe + `ON CONFLICT DO UPDATE`; `wasCreated` from probe | — |
| `Update(T)` | `UPDATE … WHERE _id=@id` | `FailedToUpdateException` if 0 rows |
| `Update(predicate, entity)` | `UPDATE … WHERE <pred> AND _id=@id` | `FailedToUpdateException` if 0 rows |
| `Update(IEnumerable)` | batched update inside a transaction | `FailedToUpdateException` on any miss |
| `Upsert(T/IEnumerable)` | `ON CONFLICT DO UPDATE`; `wasCreated` from probe | `FailedToUpsertException` if not acknowledged |
| `Delete(soft)` | set-based `json_set` + `_v+1` + `_deleted=1` | — |
| `HardDelete` | `DELETE` | — |
| `Restore` | set-based clear | `NotSupportedException` if not `ISoftDeletable` |
| `Patch` | server-side `json_set`; `$set`/`$inc`/`$unset`; `_v+1` | `FailedToUpdateException` on no match; version mismatch error |
| `JsonUpdate` | `json_patch`/merge + version enforcement + `_v` | `FailedToUpdateException` |
| `Increment` | server-side `json_extract + delta`; `_v+1` | `FailedToUpdateException` on no match |
| `Count` | `SELECT COUNT(1)` | — |
| `Exists` | `SELECT EXISTS(SELECT 1 … LIMIT 1)` | — |

`wasCreated` detection: perform a batched `SELECT _id FROM T WHERE _id IN (…)` on the same connection/transaction before the upsert, then diff. Deterministic and transaction-safe. (Alternative: `INSERT OR IGNORE` + `changes()` then `UPDATE`; documented as an internal optimization.)

Soft-delete filtering is applied centrally in `BuildQuery`/predicate composition, exactly like `ApplySoftDeleteFilter` in Mongo, honoring `includeDeleted` and `SupportsSoftDelete<TItem>()`.

`IAsyncEnumerable` results: Phase 1 materializes and wraps (`ToAsyncEnumerable`); Phase 6 streams via an async reader owned by the enumerator (connection lease held for the duration).

---

## 10. Transactions and concurrency

### 10.1 Connection model

- `SqliteRepositoryOptions.ConnectionString` uses `Microsoft.Data.Sqlite` syntax, e.g. `Data Source=app.db;Cache=Shared;Pooling=True`.
- `Microsoft.Data.Sqlite` pools connections; non-transaction operations rent/return per call.
- An active `IDatabaseTransaction` owns a dedicated `SqliteConnection` + `SqliteTransaction`; all operations passed that transaction must use its connection (`ResolveConnection(transaction)`).
- A `SemaphoreSlim(1,1)` write gate serializes write statements to avoid `SQLITE_BUSY` under the WAL single-writer constraint. Reads outside transactions can proceed concurrently (WAL).

### 10.2 PRAGMAs

On each new connection: `PRAGMA busy_timeout=@ms;` and, once per file: `PRAGMA journal_mode=WAL;`. Optionally `PRAGMA synchronous=NORMAL;`. `foreign_keys` is irrelevant (no FKs) but can be on.

### 10.3 `SqliteTransactionWrapper : IDatabaseTransaction`

```csharp
public sealed class SqliteTransactionWrapper : IDatabaseTransaction {
    internal SqliteConnection Connection { get; }
    internal SqliteTransaction Transaction { get; private set; }
    public Task Start() { Transaction = Connection.BeginTransaction(); return Task.CompletedTask; }
    public async Task CommitAsync() { await Transaction.CommitAsync(); }
    public async Task RollbackAsync() { await Transaction.RollbackAsync(); }
    public async ValueTask DisposeAsync() { await Transaction.DisposeAsync(); await Connection.DisposeAsync(); }
}
```

- `CreateTransaction()` opens a dedicated connection, returns the wrapper, acquires the write gate.
- Starting a second transaction while one is active is disallowed, or implemented with `SAVEPOINT` when `SqliteRepositoryOptions.AllowNestedTransactions` is set. (SQLite has no true nested transactions.)
- The change feed append participates in the same transaction, so `Tx_Abort_Removes_Feed_Row` passes.
- `SupportsTransactions = true` in the test fixture.

### 10.4 Rebuild / maintenance

`Rebuild()` equivalent: `VACUUM` (and `PRAGMA wal_checkpoint(TRUNCATE)`), exposed as `SqliteRepository.Rebuild()` for parity with `LiteDbRepository.Rebuild`.

---

## 11. Indexing (`IRepositoryIndexManager`)

`SqliteRepository` implements `IRepositoryIndexManager`. Each `IIndexDefinition<TItem>` becomes an expression index over `_doc`:

```sql
CREATE [UNIQUE] INDEX IF NOT EXISTS "<name>"
ON "<collection>" (<json_extract(_doc,'$.A')> [ASC|DESC], <json_extract(_doc,'$.B')> [ASC|DESC])
[WHERE <sparse predicate>];
```

Mapping:

| `IndexOptions` | SQLite |
| --- | --- |
| `Unique` | `CREATE UNIQUE INDEX` |
| `Sparse` | partial `WHERE json_extract(…) IS NOT NULL` |
| `Background` | no-op |
| `ExpireAfter`/`HasExpireAfter` | **unsupported** — skipped with an optional `Action<string> OnUnsupportedIndexOption` warning callback, or throws when `StrictTranslation`; documented. |

Name resolution: `definition.Name` if present, else `ix_<collection>_<hash-of-path-set>`. The manager also ensures the built-in indexes on `_id`/`_scope`/`_deleted`/`_scope2` exist.

`IIndexKey.Field` is `Expression<Func<TItem,object>>`; reuse `SqliteJsonPathResolver` to derive paths.

---

## 12. Scoped variants

All five scope families are implemented as partial-class files (mirroring LiteDbX/Mongo), composing predicates onto the unscoped core:

| Interface | Strategy |
| --- | --- |
| `IScopedRepository` / `IScopedReadonlyRepository` | combine `predicate.And(e => e.Scope == scope)`; set `entity.Scope = scope` before writes. Use `_scope` column in SQL. |
| `ISecondScopedRepository` / readonly | add `e.SecondScope == secondScope`; set `SecondScope`. |
| `IWeakScopedRepository` / readonly | `TItem : Entity, IScopedById`; use `ScopeModelHelper.SetScope` to handle `Ref`/`WeakRef`/`WeakRef<T>` variants; predicate via `ScopeModelHelper.BuildScopePredicate` (or direct path `$.Scope`). |
| `IWeakSecondScopedRepository` / readonly | `ISecondScopedById`; `SetSecondScope`/`BuildSecondScopePredicate`. |
| `ITransparentScopedRepository` / readonly | resolve scope from `options.TransparentScopeProvider.Invoke(typeof(TParent))`, then delegate to scoped methods. |

Reusing `ScopeModelHelper` from abstractions ensures weak/standard scoped entities serialize and query identically regardless of the ref type.

`Scopes` (multiscope) membership is handled via `json_each(_doc,'$.Scopes')`.

---

## 13. Cascade

`Saturn.Data.Sqlite.Cascade.SqliteCascadeExecutor : ICascadeExecutor`:

- `Supports(childType) => typeof(Entity).IsAssignableFrom(childType)`.
- Resolve the child collection name through an internal `SqliteRepository.CollectionNameForType(Type)` (do **not** repeat LiteDbX's broken reflection).
- Materialize candidate ids:
  - `ScopeId` present → `WHERE _scope = @parentId`
  - else `Scopes` present → `WHERE EXISTS (SELECT 1 FROM json_each(_doc,'$.Scopes') WHERE value = @parentId)`
  - else `WHERE _id = @parentId`
- `CascadeMode.SoftDelete` → set-based `json_set(IsDeleted=true, DeletedBy=parentId)` + `_v+1`.
- `CascadeMode.Archive` → `json_set(IsArchived=true, ArchivedBy=parentId)` + `_v+1`.
- `CascadeMode.HardDelete` → `DELETE`.
- Return `CascadeStepResult` with affected ids; shared-scope semantics preserved by `CascadeEngine`.

`SqliteRepository.Cascade.cs` implements `DeleteCascade`/`HardDeleteCascade` by delegating to a `CascadeEngine` constructed with the SQLite executor (mirroring Mongo/LiteDbX wiring).

---

## 14. Change feed

`Saturn.Data.Sqlite.ChangeFeed.SqliteOutboxChangeFeedSink : OutboxChangeFeedSink`:

Tables:

```sql
CREATE TABLE IF NOT EXISTS "__change_feed" (
    seq          INTEGER NOT NULL PRIMARY KEY,
    change_id    TEXT    NOT NULL,
    source       TEXT    NOT NULL,
    entity_type  TEXT    NOT NULL,
    occurred_utc TEXT    NOT NULL,
    operation    TEXT    NOT NULL,
    outcome      TEXT    NOT NULL,
    entity_ids   TEXT    NOT NULL,   -- JSON array of ids
    is_partial   INTEGER NOT NULL,
    has_full_items INTEGER NOT NULL,
    items_json   TEXT    NULL,
    version      INTEGER NULL
);
CREATE INDEX IF NOT EXISTS ix___change_feed_source_seq ON "__change_feed"(source, seq);

CREATE TABLE IF NOT EXISTS "__change_feed_counters" (
    source      TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    seq         INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (source, entity_type)
);
```

`NextSequenceAsync` (atomic, transaction-aware):

```sql
INSERT INTO "__change_feed_counters"(source, entity_type, seq)
VALUES (@s, @t, 1)
ON CONFLICT(source, entity_type) DO UPDATE SET seq = seq + 1
RETURNING seq;
```

`AppendRowAsync` inserts a row mapped with `ChangeFeedRecordMapper.ToRecord`.
`ReadRowsAsync` selects `WHERE source=@s AND seq > @after ORDER BY seq LIMIT @take` and maps with `ToEvent`.

DI helper:

```csharp
public static IServiceCollection AddSqliteChangeFeed(this IServiceCollection services, SqliteConnection connection, string source);
```

registers `IChangeFeedSink` + `ChangeFeedPoller`, mirroring `AddLiteDbChangeFeed`.

Because the sink appends inside `context.Transaction`, `Tx_Abort_Removes_Feed_Row` passes. Because `BehaviorDispatcher.DispatchAfterAsync` swallows after-hook exceptions (`BehaviorDispatcher.cs:113-116`), `Feed_Error_Does_Not_Fail_Repository_Method` passes without extra work.

---

## 15. Options and DI

```csharp
public sealed class SqliteRepositoryOptions {
    public string ConnectionString { get; set; }               // Microsoft.Data.Sqlite syntax
    public bool UseJsonB { get; set; }                          // store _doc as JSONB BLOB
    public bool StrictTranslation { get; set; } = true;         // fail on untranslatable predicates
    public bool UseJsonMergePatch { get; set; }                 // Patch uses json_patch
    public bool EnableWal { get; set; } = true;
    public int BusyTimeoutMs { get; set; } = 5000;
    public bool AllowNestedTransactions { get; set; }           // SAVEPOINT-based
    public bool CreateTablesOnDemand { get; set; } = true;
    public bool ServerSideProjection { get; set; }              // Phase 6
    public EntityJsonSerializerOptions Serializer { get; set; } = new();
    public Action<string> OnUnsupportedIndexOption { get; set; } // warning sink (e.g. TTL)
}
```

`SqliteRepository` constructors:

```csharp
public SqliteRepository(RepositoryOptions repositoryOptions, SqliteRepositoryOptions sqliteOptions);
internal SqliteRepository(RepositoryOptions repositoryOptions, SqliteRepositoryOptions sqliteOptions, SqliteConnection existingConnection); // testing
```

DI (`ServicesExtensions`, ServiceScan partial, mirroring Mongo/LiteDbX):

```csharp
[GenerateServiceRegistrations(
    TypeNameFilter = "*Repository",
    AsImplementedInterfaces = true,
    AsSelf = true,
    Lifetime = ServiceLifetime.Singleton)]
public static partial IServiceCollection AddSaturnSqliteRepositoryServices(this IServiceCollection services);
```

Singleton lifetime matches the other providers (one file-backed repository instance per service provider). Multi-database scenarios construct additional `SqliteRepository` instances explicitly.

---

## 16. Project layout and packages

```
Saturn.Data.Sqlite/
├─ Saturn.Data.Sqlite.slnx  (or add to Saturn.Data.slnx)
├─ Saturn.Data.Sqlite/
│  ├─ Saturn.Data.Sqlite.csproj
│  ├─ SqliteRepository.cs
│  ├─ SqliteRepository.ReadonlyRepository.cs
│  ├─ SqliteRepository.Repository.cs
│  ├─ SqliteRepository.Scoped.cs
│  ├─ SqliteRepository.ScopedReadonly.cs
│  ├─ SqliteRepository.SecondScope.cs
│  ├─ SqliteRepository.SecondScopedReadonly.cs
│  ├─ SqliteRepository.WeakScoped.cs
│  ├─ SqliteRepository.WeakScopedReadonly.cs
│  ├─ SqliteRepository.WeakSecondScoped.cs
│  ├─ SqliteRepository.WeakSecondScopedReadonly.cs
│  ├─ SqliteRepository.TransparentScoped.cs
│  ├─ SqliteRepository.TransparentScopedReadonly.cs
│  ├─ SqliteRepository.Cascade.cs
│  ├─ SqliteRepositoryOptions.cs
│  ├─ SqliteEntityStore.cs
│  ├─ SqliteDataUpdateDefinition.cs
│  ├─ SqliteTransactionWrapper.cs
│  ├─ ServicesExtensions.cs
│  ├─ Serialization/
│  │  ├─ EntityJsonSerializer.cs
│  │  ├─ EntityJsonSerializerOptions.cs
│  │  ├─ RefJsonConverter.cs
│  │  ├─ WeakRefJsonConverter.cs
│  │  ├─ PropertiesJsonConverter.cs
│  │  └─ EntityJsonTypeInfoResolver.cs
│  ├─ Query/
│  │  ├─ SqliteJsonPathResolver.cs
│  │  ├─ SqliteExpressionTranslator.cs
│  │  ├─ SqliteTranslationException.cs
│  │  ├─ SqlFragment.cs
│  │  ├─ SqliteQueryBuilder.cs
│  │  ├─ SqliteQueryable.cs
│  │  └─ SqliteQueryProvider.cs
│  ├─ Indexes/SqliteIndexManager.cs
│  ├─ Cascade/SqliteCascadeExecutor.cs
│  └─ ChangeFeed/
│     ├─ SqliteOutboxChangeFeedSink.cs
│     └─ ChangeFeedServicesExtensions.cs
├─ Saturn.Data.Sqlite.Tests/
│  ├─ Saturn.Data.Sqlite.Tests.csproj
│  ├─ DatabaseFixture.cs
│  ├─ UnitTestableSqliteRepository.cs
│  ├─ BasicTests.cs
│  ├─ ScopedTests.cs
│  ├─ ChangeFeedTestFixture.cs
│  ├─ ChangeFeedAfterWriteTests.cs
│  ├─ CascadeTestFixture.cs
│  └─ ProviderSpecificTests.cs
└─ Saturn.Data.Sqlite.Playground/   (optional)
   └─ Program.cs
```

`Saturn.Data.Sqlite.csproj` (pattern-matched to the other providers):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Version>7.0.0</Version>
    <PackageId>GoLive.Saturn.Data.Sqlite</PackageId>
    <Authors>SurgicalCoder</Authors>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <Description>SQLite (JSON document) provider for Saturn.Data.</Description>
    <Copyright>Copyright 2020-2026 - SurgicalCoder</Copyright>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <GenerateRepositoryUrlAttribute>true</GenerateRepositoryUrlAttribute>
    <PackOnBuild>true</PackOnBuild>
    <PackageProjectUrl>https://github.com/surgicalcoder/Saturn.Data</PackageProjectUrl>
    <RepositoryUrl>https://github.com/surgicalcoder/Saturn.Data</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions.csproj" />
    <ProjectReference Include="..\..\Saturn.Data.Entities\GoLive.Saturn.Data.Entities\GoLive.Saturn.Data.Entities.csproj" />
    <ProjectReference Include="..\..\Saturn.Data.Entities\Saturn.Data.Entities.JsonConverters\Saturn.Data.Entities.JsonConverters.csproj" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
    <PackageReference Include="ServiceScan.SourceGenerator" Version="3.2.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

Pin `Microsoft.Data.Sqlite` to the exact GA patch used by the repo's other `10.0.x` Microsoft packages at implementation time.

Also update:
- `Saturn.Data.slnx` — add a `/Sqlite/` folder with the three projects.
- `README.md` — provider/package tables, prerequisites, test command.
- `.github/workflows/publish-changed-nugets.yml` consumes `scripts/detect-changed-projects.ps1`, which maps changed files to projects automatically; no workflow edit needed beyond the solution.

---

## 17. Testing strategy

### 17.1 Shared contract tests

Consume the existing provider-agnostic suites via a fixture:

```csharp
public sealed class SqliteDatabaseFixture
    : IRepositoryTestFixture<UnitTestableSqliteRepository>,
      ICascadeTestFixture<UnitTestableSqliteRepository>,
      IChangeFeedTestFixture,
      IDisposable
{
    public UnitTestableSqliteRepository Repository { get; }
    public RecordingWriteBehavior Recorder { get; }
    public IChangeFeedSink Sink { get; }
    public bool SupportsTransactions => true;
    public IList<IRepositoryWriteBehavior> WriteBehaviors => Repository.Options.WriteBehaviors;
    // temp file DB under Path.GetTempPath(); Dispose drops/removes file
}
```

Suites to inherit:

- `BasicRepositoryContractTests<,>` — Create/Update/Save/Upsert.
- `ScopedRepositoryContractTests<,>` — scoped basics.
- `ComprehensiveScopedRepositoryContractTests<,>` — full scoped matrix.
- `CascadeContractTests<,>` — archive/hard delete/transitive/sibling.
- `ChangeFeedContractTests<,>` and `ChangeFeedPollerTests<,>` — ordering, watermark, replay, type filter, tx abort, error isolation, `$set` patch, increment.

DB path: a fresh temp file per fixture instance (mirrors LiteDbX/Stellar fixtures which use `e:\_scratch` / local paths). Prefer `Path.Combine(Path.GetTempPath(), $"saturn-sqlite-{Guid.NewGuid():N}.db")`. Also run one suite against `Data Source=:memory:;Cache=Shared` with a keepalive connection to prove the pooling model.

### 17.2 Provider-specific tests

- JSON1 probe: `SELECT json_extract('{"a":1}','$.a')` returns 1; fail loudly if absent.
- Persisted document shape: serialized `_doc` keys exclude `Changes`/`EnableChangeTracking`/`_shortId`; `Scope` is a JSON string.
- `Ref<T>` / `WeakRef` round-trip.
- `HashedString`/`EncryptedString` round-trip and `Populate`/`CompareHash`/decrypt.
- Index existence: `PRAGMA index_list('ChildEntity')` contains a `EnsureIndexes`-created index; unique violation raised.
- `IRepositoryIndexManager` with a TTL option emits a warning and does not throw.
- Translator unit tests per supported node; unsupported node → `StrictTranslation` throws / fallback behaves.
- `Scopes` array membership (`json_each`).
- Transaction rollback leaves no row and no outbox row; commit persists.
- WAL concurrency: two reader connections observe committed writes.
- `Rebuild()` (VACUUM) succeeds.
- Continuation stability with `Id` ascending; continuation suppressed under other sorts.
- Increment/patch version expectations (`expectedVersion` mismatch).

---

## 18. Performance considerations

- Expression indexes on `json_extract` make common predicates index-seeking (verify with `EXPLAIN QUERY PLAN`; tests assert index usage on the primary scope path).
- Shadow `_scope`/`_deleted` avoid JSON parsing for the hottest filters.
- Set-based bulk writes inside a transaction amortize fsync (WAL + `synchronous=NORMAL`).
- `IAsyncEnumerable` streaming (Phase 6) avoids whole-collection materialization.
- In-memory fallback is the correctness escape hatch and the known perf cliff; `StrictTranslation` is the production recommendation.
- `Count` uses `COUNT(1)`, not row hydration.
- JSONB (`UseJsonB`) reduces parse cost and file size at the expense of tool readability.
- Cursor pagination (`continueFrom`) avoids `OFFSET` scans for large collections.

---

## 19. Security considerations

- Default to **parameterized** SQL everywhere. No string interpolation of values; identifiers (collection/index names) are validated/quoted with double-quote escaping and a strict `[A-Za-z0-9_]` allow-list via `GetCollectionName`.
- `SqliteRepositoryOptions.ConnectionString` may contain a file path; do not log it.
- `EncryptedString` uses `GoLive.Saturn.Crypto` (same as Mongo/LiteDbX); never log decoded values.
- File-level encryption (SQLCipher) is out of scope; documented so it is a conscious decision.
- Do not enable `SQLITE_ENABLE_LOAD_EXTENSION`; JSON functions are built-in and sufficient.

---

## 20. Risks and mitigations

| # | Risk | Impact | Mitigation |
| --- | --- | --- | --- |
| R1 | Expression translation gaps | Wrong/failed queries | Explicit supported-node set, per-node unit tests, in-memory fallback, `StrictTranslation` for prod |
| R2 | In-memory fallback perf cliff | Slow large scans | Document; log/telemetry counter; prefer indexable predicates; Phase 6 projections |
| R3 | SQLite single-writer contention | `SQLITE_BUSY` under load | WAL, `busy_timeout`, write semaphore, transactions batched |
| R4 | JSON1 not compiled in on some builds | Feature failure | Startup probe + clear exception; `Microsoft.Data.Sqlite` bundles JSON1 by default |
| R5 | DateTime/decimal fidelity in JSON | Round-trip/compare bugs | "O" format ISO-8601; decimal number (or string mode); tests |
| R6 | `object`/polymorphic properties lose type | Data fidelity | Document; `PropertiesJsonConverter`; optional `$type` discriminator (Phase 7) |
| R7 | Shipped `HashedString`/`EncryptedString` STJ converters persist only `Populated` | Secrets lost on round-trip | Provider supplies full-fidelity converters (`Hash`/`Salt`/`Encoded`) consistent with Mongo/LiteDbX, or fixes the package converters |
| R8 | `IQueryable` semantics differ from Mongo/LiteDbX | Surprise in consumer code | Minimal provider with transparent LINQ-to-Objects fallback; document |
| R9 | `Version` null vs 0 semantics | Optimistic concurrency mismatches | Normalize `_v` handling to Mongo/LiteDbX (`COALESCE`, missing ⇒ null); contract tests |
| R10 | Continuation under non-`Id` sort silently ignored | Caller confusion | Same `CanApplyContinuation` guard as Mongo/LiteDbX; documented |
| R11 | LiteDbX cascade executor is broken (reflection on missing `CollectionFor`) | Don't inherit bug | SQLite exposes a real internal collection-name resolver; cascade tests |
| R12 | Pushing work to `AlterTable` as features grow | Migration complexity | `__saturn_schema.schema_version` + idempotent `ALTER TABLE ADD COLUMN` migrations |
| R13 | `HashedString` implicit conversion / null refs in translator | Expr translation NREs | Defensive translator + tests mirroring `RefExpressionRewriter` concerns |
| R14 | Background/TTL index options silently ignored | Silent behavior change | Warning callback + docs; `StrictTranslation` can throw |

---

## 21. Alternatives considered

| Alternative | Why not |
| --- | --- |
| **Single `documents` table** (`collection` + `_id` + `_doc`) | Simpler DDL, but hot-path indexes must be `(collection, json_extract(...))` composite, index naming collides, table-sized scans for uncorrelated types, and it diverges from Mongo/LiteDbX collection isolation. Table-per-collection is closer to the existing mental model. |
| **Shredded columns per known property** | Explicitly contradicts "no fixed columns"; schema churn on every entity change. Rejected. Generated columns could be offered as a per-type optimization later. |
| **EF Core + `Microsoft.EntityFrameworkCore.Sqlite` JSON-owned types** | Heavy dependency, model-first assumptions, mismatched with the dynamic `Entity` model, and hard to align with `Ref`/scope/soft-delete semantics. Rejected. |
| **Newtonsoft.Json instead of System.Text.Json** | The entities repo already ships STJ converters; Stellar uses STJ for patch. STJ is the house default. |
| **`System.Data.SQLite`** | Heavier native story; `Microsoft.Data.Sqlite` is the modern, cross-platform, actively maintained option and pools connections. |
| **No shadow columns (pure JSON paths only)** | Correct but slower for the universal scope/soft-delete predicates; expression indexes would be larger and less selective. Shadow columns are a pragmatic projection of framework-wide concepts only. |
| **No server-side translation (always client-side)** | Simplest, but loses the entire value of SQLite for filtering/indexes and makes `Many`/`Count` unbounded. Used only as fallback. |

---

## 22. Phased implementation plan

Each phase ends with a green build and the stated exit criteria.

### Phase 0 — Scaffolding
- Create `Saturn.Data.Sqlite` + `.Tests` projects; add to `Saturn.Data.slnx`.
- `SqliteRepositoryOptions`, `ServicesExtensions`, `SqliteRepository` ctor/connection/PRAGMAs.
- JSON1 probe; `SqliteEntityStore` DDL + schema table.
- Test fixture + `UnitTestableSqliteRepository.DropRecreateDatabase()`.
- **Exit:** solution builds; JSON1 probe passes; empty shared suites compile.

### Phase 1 — Serialization + core CRUD (unscoped)
- `EntityJsonSerializer`, resolver, converters (`Ref`, `WeakRef`, `Properties`).
- Store read/write (insert/update/delete/query by id) and set-based delete/hard delete/restore.
- `Insert/Save/Update/Upsert/Delete/HardDelete/Restore/ById/ByIds/Count/Exists`.
- Soft-delete filtering.
- **Exit:** `BasicRepositoryContractTests` green.

### Phase 2 — Query translation
- `SqliteJsonPathResolver`, `SqliteExpressionTranslator`, `SqlFragment`, `SqliteQueryBuilder`.
- `All/Many/One/Random`, sorting, pagination, `continueFrom`, `whereClause`, `IQueryable`.
- **Exit:** `ComprehensiveScopedRepositoryContractTests` (unscoped parts) + translator unit tests green.

### Phase 3 — Scoped variants
- All `IScoped*`, `ISecondScoped*`, `IWeak*`, `ITransparentScoped*` partials using `ScopeModelHelper`.
- **Exit:** `ScopedRepositoryContractTests` + `ComprehensiveScopedRepositoryContractTests` fully green.

### Phase 4 — Mutation semantics + indexes
- `Patch` (`$set`/`$inc`/`$unset`), `JsonUpdate`, `Increment`, `SqliteIndexManager`, `EnsureIndexes`.
- **Exit:** `ChangeFeedContractTests` patch/increment cases green; index tests green.

### Phase 5 — Transactions + concurrency
- `SqliteTransactionWrapper`, write gate, pooled reads, optional savepoints, `Rebuild()`.
- **Exit:** tx rollback/commit tests, WAL concurrency tests, `Tx_Abort_Removes_Feed_Row` green.

### Phase 6 — Cascade + change feed + streaming/projection
- `SqliteCascadeExecutor`, `SqliteRepository.Cascade.cs`, `SqliteOutboxChangeFeedSink`, `AddSqliteChangeFeed`.
- Optional: streaming `IAsyncEnumerable`, server-side projection, JSONB mode.
- **Exit:** `CascadeContractTests`, `ChangeFeedPollerTests`, full `ChangeFeedContractTests` green.

### Phase 7 — Hardening, docs, packaging
- Performance pass (`EXPLAIN QUERY PLAN` assertions, pooling, batch sizes).
- `README.md` update; package metadata/version; publish workflow validation.
- Resolve R7 (full-fidelity crypto converters) and finalize open decisions.
- **Exit:** all three provider test projects green; package packs; no behavioral regressions in Mongo/LiteDbX/Stellar.

---

## 23. Definition of done / acceptance criteria

- `dotnet build Saturn.Data.slnx -c Release` succeeds with the new projects.
- `dotnet test Saturn.Data.Sqlite.Tests` passes all inherited shared suites plus provider-specific tests.
- No changes required to `GoLive.Saturn.Data.Abstractions`, `GoLive.Saturn.Data.Entities`, or existing providers (additive only).
- All 14 requirement areas in §2 are demonstrably implemented.
- Public API mirrors the other providers: `SqliteRepository`, `SqliteRepositoryOptions`, `SqliteDataUpdateDefinition<T>`, `AddSaturnSqliteRepositoryServices`, `AddSqliteChangeFeed`, `SqliteCascadeExecutor`.
- Package `GoLive.Saturn.Data.Sqlite` builds/packs with `GeneratePackageOnBuild`.
- Documentation: this proposal + README update; unsupported features (TTL indexes, SQLCipher, change streams) explicitly listed.

---

## 24. Open decisions

1. **Package/namespace name:** `GoLive.Saturn.Data.Sqlite` (recommended) vs `GoLive.Saturn.Data.Sqlite.Json`.
2. **`_doc` default storage:** TEXT JSON (recommended, tool-friendly) vs BLOB JSONB.
3. **`StrictTranslation` default:** `true` (recommended) vs `false` (fallback-friendly).
4. **`Scopes` handling for multiscope:** `json_each` subquery (recommended) vs a `_scopes` denormalized JSON + expression index.
5. **`ScopeId` persistence:** exclude derived (recommended) vs store both `Scope` and `ScopeId`.
6. **Crypto converter ownership:** fix `Saturn.Data.Entities.JsonConverters` to persist `Hash`/`Salt`/`Encoded` vs add provider-local converters (recommended provider-local first to avoid cross-package churn).
7. **Connection model default:** pooled per-operation (recommended) vs single shared connection with a global gate.
8. **Polymorphic `$type` discriminator:** Phase 7 vs never.

---

## 25. Appendix A — JSON path / SQL quick reference

| C# | SQL fragment |
| --- | --- |
| `x.Id` | `_id` |
| `x.Version` | `_v` |
| `x.Scope`, `x.ScopeId` | `json_extract(_doc,'$.Scope')` |
| `x.SecondScope`, `x.SecondScopeId` | `json_extract(_doc,'$.SecondScope')` |
| `x.Scopes` | `json_extract(_doc,'$.Scopes')` / `json_each(_doc,'$.Scopes')` |
| `x.Name == "a"` | `json_extract(_doc,'$.Name') = @p0` |
| `x.Count > 3` | `json_extract(_doc,'$.Count') > @p0` |
| `x.Name.Contains("a")` | `instr(json_extract(_doc,'$.Name'), @p0) > 0` |
| `ids.Contains(x.Id)` | `_id IN (SELECT value FROM json_each(@p0))` |
| `x.Scopes.Contains(s)` | `EXISTS (SELECT 1 FROM json_each(_doc,'$.Scopes') WHERE value = @p0)` |
| `!x.IsDeleted` | `json_extract(_doc,'$.IsDeleted') = 0` |
| `x.DeletedAt == null` | `json_extract(_doc,'$.DeletedAt') IS NULL` |

## 26. Appendix B — Mapping of abstraction members to SQLite features

| Abstraction | SQLite mechanism |
| --- | --- |
| `IReadonlyRepository` | `SELECT` + JSON1, `IAsyncEnumerable` |
| `IRepository` | `INSERT`/`UPDATE`/`DELETE` + `json_set`/`json_patch` |
| `IDatabaseTransaction` | `SqliteTransaction` (real), `BEGIN`/`COMMIT`/`ROLLBACK` |
| `IRepositoryIndexManager` | `CREATE INDEX … ON (json_extract(...))` |
| `ICascadeExecutor` | scope predicates + set-based updates/deletes |
| `IChangeFeedSink` | outbox + counters tables (transaction-aware) |
| `IDataUpdateDefinition<T>` | `SqliteDataUpdateDefinition<T>` (server-side `$set`) |
| `RepositoryOptions.GetCollectionName` | table name |
| `SortOrder<T>` | `ORDER BY json_extract(...)` + `_id` tiebreak |
| `includeDeleted` | `_deleted` column filter |
| `Version` | `_v` column + `WHERE _v=@expected` |
| `Properties` | `$.Properties` object via `PropertiesJsonConverter` |

## 27. Appendix C — Recommended SQLite extensions for .NET

This appendix is a decision aid for which native/extension capabilities to adopt in the SQLite provider. None are required for the baseline proposal; each is opt-in and isolated behind `SqliteRepositoryOptions`.

### 27.1 Native provider bundles (`SQLitePCLRaw`)

`Microsoft.Data.Sqlite` is a managed wrapper; the actual SQLite engine comes from a `SQLitePCLRaw` bundle. The bundle choice determines JSON1, JSONB, FTS5, and encryption availability.

| Bundle package | Engine | JSON1/JSONB | FTS5 | Encryption | License/cost |
| --- | --- | --- | --- | --- | --- |
| `SQLitePCLRaw.bundle_e_sqlite3` (default via `Microsoft.Data.Sqlite`) | SQLite | yes (3.38+/3.45+) | yes | no | MIT |
| `SQLitePCLRaw.bundle_e_sqlcipher` | SQLCipher (community build) | yes | yes | yes (AES-256) | MIT/BSD (community) |
| `SQLitePCLRaw.bundle_zetetic` | SQLCipher (Zetetic, official) | yes | yes | yes (AES-256) + FIPS/support | **commercial** license |
| `SQLitePCLRaw.bundle_green` | system-provided SQLite | depends on OS | depends | no | varies — **not recommended** (non-deterministic features) |
| `Microsoft.Data.Sqlite.Core` | none (must pair with a bundle) | — | — | — | MIT |

Recommendation: keep `Microsoft.Data.Sqlite` (default `e_sqlite3`) for the standard provider. If encryption is required, switch to `Microsoft.Data.Sqlite.Core` **plus exactly one** encryption bundle and initialize `SQLitePCL.Batteries_V2.Init()`. Do not reference both `Microsoft.Data.Sqlite` and `.Core`.

### 27.2 Encryption

Two independent layers; use both for defense in depth.

**A. Whole-file encryption (SQLCipher).**

- Swap `Microsoft.Data.Sqlite` → `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlcipher` (free community) or `bundle_zetetic` (paid, FIPS + support).
- `Microsoft.Data.Sqlite` exposes `SqliteConnectionStringBuilder.Password`, which issues `PRAGMA key` against SQLCipher. Runtime rotation is done with an explicit `PRAGMA rekey` statement (verify the pinned version's managed API surface before relying on a wrapper method).
- Add `SqliteRepositoryOptions.EncryptionPasswordProvider` (`Func<string>` or `Func<CancellationToken, ValueTask<string>>`) rather than a raw password string, so keys come from a secret store and are never logged or captured into the connection string in plain form.
- Rotate with `PRAGMA rekey='new'` (wrap as `SqliteRepository.Rekey(newPassword)`).
- Compatible with WAL and the rest of the design. Costs: larger native binary per RID, slightly slower IO, and the DB file is opaque to CLI tooling unless opened with the key.
- Not a silver bullet: it does **not** protect against a compromised process, memory dumps, or an attacker holding the key. It protects at-rest copies, backups, and stolen devices.
- Note: SQLCipher changes default page size/KDF; if you later migrate a plaintext DB, use `sqlcipher_export()`.

**B. Field-level encryption (already in-repo).**

- `EncryptedString` + `GoLive.Saturn.Crypto` (Mongo/LiteDbX already serialize these). This is the recommended default for schema-level sensitive fields.
- Searchable equality is possible only via `HashedString` (deterministic hash) — never by decrypting in SQL. Design queries accordingly (`HashedString` for lookup, `EncryptedString` for retrieval).
- **Blocker to resolve first (proposal R7):** the shipped STJ converters `EncryptedStringJsonConverter`/`HashedStringJsonConverter` persist only `Populated`, dropping `Hash`/`Salt`/`Encoded`. The provider needs full-fidelity converters (or a fix to `Saturn.Data.Entities.JsonConverters`) before claiming field-level crypto parity.

Recommendation: implement `EncryptedString`/`HashedString` fidelity in Phase 4; expose SQLCipher as an opt-in configuration, not the default, because it complicates tooling and distribution.

### 27.3 Full-text search (FTS5)

FTS5 is compiled into `bundle_e_sqlite3`; no extra package. Highest-value optional feature for a document store.

- Create an external-content FTS table mirroring selected JSON paths:
  ```sql
  CREATE VIRTUAL TABLE "<collection>_fts" USING fts5(
      Name, Body,
      content='<collection>', content_rowid='rowid'
  );
  ```
  or a contentless FTS table synced from `_doc` values on write.
- Keep it in sync either with SQL triggers on the base table, or from the repository's write pipeline (Inside `SqliteEntityStore.WriteAsync`) so it participates in the same transaction.
- Query with `MATCH` and rank with `bm25()`.
- Expose as `SqliteRepository.SearchAsync<TItem>(string query, …)` rather than trying to force it through LINQ `Contains`, which would be a full scan.
- Alternative for non-tokenized substring search: keep `instr(json_extract(...))` from the main design (already implemented for `string.Contains`).

Recommendation: add FTS5 in Phase 7 as `SqliteRepositoryOptions.EnableFullTextSearch` with a per-type indexed-field list.

### 27.4 Vector search (`sqlite-vec`)

- `sqlite-vec` (successor to `sqlite-vss`) adds `vec0` virtual tables and KNN over float/int8 embeddings.
- Distributed as a loadable native extension; community .NET wrappers exist but be prepared to bring-your-own native binary and register it via `sqlite3_auto_extension`/`enable_load_extension`.
- Extension loading in `Microsoft.Data.Sqlite` requires the underlying provider to allow it and the version's loading API to be available — verify against the pinned `Microsoft.Data.Sqlite` version before committing.
- Useful only if the provider needs embedding/RAG-style search; otherwise skip (native binary + RID matrix cost).

Recommendation: out of scope for v1; note it as a future capability behind an `EnableVectorSearch` option.

### 27.5 Compression

- **Application-level:** gzip/zstd/brotli the serialized `_doc` before binding, set `SqliteRepositoryOptions.Compression`. Zero native dependencies, works for any provider host. Cost: no `json_extract` pushdown over compressed docs (translator must fall back to in-memory for that collection), so only apply to cold/write-heavy, rarely-queried collections.
- **Transparent `sqlite-zstd`:** a loadable extension that dictionary-compresses pages/columns transparently and keeps JSON queryable. Requires the native extension + zstd and extension loading.
Recommendation: application-level compression as an opt-in for archival collections only; skip `sqlite-zstd` unless profiling justifies it.

### 27.6 Utility extension packs (`sqlean` et al.)

`sqlean` bundles `crypto`, `uuid`, `regexp`, `fuzzy`, `text`, `stats`, `time`, `ipaddr`, `vsv`. Potentially useful: `regexp` (LIKE/regex predicates), `uuid` (ids), `crypto` (hashing). All are loadable extensions and overlap with `GoLive.Saturn.Crypto` and .NET-side processing.

Recommendation: skip by default; add specific functions only if a concrete query needs them, and prefer doing crypto/hashing in .NET so behavior matches the other providers.

### 27.7 Built-in capabilities worth using (no extra package)

| Capability | Use |
| --- | --- |
| JSONB (3.45+) | Compact `_doc` storage (`UseJsonB`) |
| `VACUUM INTO 'file'` | Consistent file snapshot/backup |
| `SqliteConnection.BackupDatabase` | Online backup to another connection |
| `PRAGMA wal_checkpoint(TRUNCATE)` | Bound WAL growth during maintenance |
| `PRAGMA optimize` | Periodic planner/statistics refresh |
| `RETURNING` (3.35+) | Atomic upsert/sequence reads |
| `EXPLAIN QUERY PLAN` | Index-usage assertions in tests |
| Math functions | Optional for predicates where the bundle enables them (verify; not guaranteed in `e_sqlite3`) |

### 27.8 Recommendation summary

| Priority | Extension | Rationale |
| --- | --- | --- |
| Must (v1) | JSON1 + JSONB | Core storage and query model |
| Must (v1) | `EncryptedString`/`HashedString` full-fidelity converters | Closes R7; field-level secrets |
| Should (v1, opt-in) | SQLCipher via `Microsoft.Data.Sqlite.Core` + `bundle_e_sqlcipher` | Whole-file at-rest encryption, free; `bundle_zetetic` if FIPS/support is required |
| Should (post-v1) | FTS5 | Search is the most valuable native feature for documents |
| Nice (post-v1) | App-level `_doc` compression | Archival collections |
| Later | `sqlite-vec` | Only if embeddings/RAG are in scope |
| Skip | `sqlean`, `sqlite-zstd`, `bundle_green` | Overlap with .NET logic or non-deterministic |

### 27.9 Licensing and distribution notes

- `bundle_zetetic` (SQLCipher official) requires a commercial license; the free path is `bundle_e_sqlcipher` (community).
- Shipping native engines means per-RID assets; confirm the CI publish workflow packs the correct RIDs for the target frameworks.
- Encryption keys must never be placed in connection strings that get logged, in `SqliteRepositoryOptions` `ToString()`, or in exception messages.
- FTS/vector/utility extensions that are **loadable** require extension loading to be enabled and may not be available on all platforms/hosts; gate them behind feature options and fail with an actionable message when unavailable.

## 28. Appendix D — WAL configuration checklist

Yes, WAL needs deliberate configuration; WAL is not "set and forget". The items below are concrete requirements for the SQLite provider.

### 28.1 What is and is not persistent per-connection

| Setting | Scope | Persistent in file? | Notes |
| --- | --- | --- | --- |
| `journal_mode = WAL` | database file | **yes** | Set once; stays WAL across connections and restarts. Must be run **outside** a transaction. Best done during schema init before concurrency. |
| `synchronous` | connection | no | Must be set per physical connection. |
| `busy_timeout` | connection | no | Must be set per physical connection. |
| `wal_autocheckpoint` | connection | no | Per connection; default 1000 pages. |
| `journal_size_limit` | connection | no | Caps post-checkpoint WAL truncation size. |
| `locking_mode` | connection | no | Leave `NORMAL`. |

**Pooling trap:** `Microsoft.Data.Sqlite` pools connections by default, so "per connection" PRAGMAs you execute once may not be in effect on the next physical connection handed out. There is no per-open hook. Mitigations, in order of preference:

1. Put the busy timeout in the connection string (`Default Timeout=30`); the provider drives `sqlite3_busy_timeout` for command execution. Verify the value explicitly with `PRAGMA busy_timeout` in a probe test.
2. Disable pooling (`Pooling=False`) and run a `ConfigureConnection(SqliteConnection)` after every `OpenAsync()` that sets `busy_timeout`, `synchronous`, `journal_size_limit`, `wal_autocheckpoint`.
3. Keep pooling but have `SqliteRepository` own a small number of long-lived pinned connections whose PRAGMAs are configured at construction.

Recommendation: (1) for the default, plus (2) when `SqliteRepositoryOptions.ConfigureConnection` is user-supplied.

### 28.2 Startup sequence

On first use of a database (schema init), before any concurrent work:

```sql
PRAGMA journal_mode = WAL;         -- persistent; assert the result equals "wal"
PRAGMA synchronous  = NORMAL;      -- recommended for WAL
PRAGMA busy_timeout = 5000;        -- per connection
PRAGMA wal_autocheckpoint = 1000;  -- pages (~4 MB at 4 KiB pages)
PRAGMA journal_size_limit = 67108864; -- 64 MiB cap after checkpoint
PRAGMA optimize;                   -- periodically, not just at open
```

`journal_mode=WAL` returns the effective mode; if it returns `delete` (e.g., `:memory:` or a filesystem that cannot support WAL), log/throw according to `SqliteRepositoryOptions.RequireWal`.

### 28.3 Writers must use `BEGIN IMMEDIATE`

This is the most important correctness item. A deferred (`BEGIN`) transaction that reads and then tries to write can fail with `SQLITE_BUSY_SNAPSHOT` (extended code 261) if another connection committed in the meantime. `busy_timeout` does **not** retry that code — it is a logical deadlock, not a lock wait.

- `SqliteTransactionWrapper.Start()` must open the transaction as **immediate**: `connection.BeginTransaction(deferred: false)` where available, otherwise execute `BEGIN IMMEDIATE` directly.
- Alternatively, explicit single-statement writes inside the write gate are implicitly immediate; the issue only arises for multi-statement transactions.
- Document that any user-supplied transaction intended to write should be immediate.

### 28.4 Busy handling and retry

WAL removes reader/writer blocking, but not writer/writer. Two sources of contention remain:

- Writer vs writer → `SQLITE_BUSY`.
- Checkpoint vs writer/reader → `SQLITE_BUSY`.

Implement a bounded retry with exponential backoff+jitter around write operations, mapping `SqliteException.SqliteErrorCode == 5 (BUSY)` / `6 (LOCKED)`. Do not retry `SQLITE_BUSY_SNAPSHOT` blindly; fix the transaction mode (§28.3) or retry the whole transaction from scratch.

The in-process write gate (§10.1) eliminates intra-process writer contention; `busy_timeout` + retry covers other processes.

### 28.5 Checkpointing and WAL growth

- WAL grows to `wal_autocheckpoint` pages then auto-checkpoints on commit; `journal_size_limit` caps the retained WAL file size.
- **Long-lived read transactions starve checkpointing.** A reader holding a snapshot prevents frames being reclaimed, so the WAL grows. This directly affects the Phase 6 streaming `IAsyncEnumerable` design: a streamed read holds a connection/read transaction open for the duration of enumeration. Either:
  - keep streaming reads short and dispose deterministically, or
  - materialize large result sets instead of streaming, or
  - accept growth and checkpoint during maintenance.
- `Rebuild()`/maintenance should run `PRAGMA wal_checkpoint(TRUNCATE);` and then `VACUUM;`.
- On clean shutdown of the last connection, SQLite checkpoints and removes the WAL. With pooling, a returned connection is not closed, so add an explicit checkpoint on repository dispose / `ClearPool`.

### 28.6 Filesystem and deployment constraints

- **WAL needs the directory writable** (it creates `<db>-wal` and `<db>-shm`), even for read-only usage. Read-only connections need directory write permission, or `immutable=1` when it is guaranteed no writer exists.
- **Network filesystems (SMB/NFS/DFS) are not reliable with WAL** because of shared-memory (`-shm`) mmap and locking semantics. If the database lives on a network share, fall back to `journal_mode=DELETE` or keep the DB local.
- WAL is multi-process safe on a local filesystem; that is a feature for desktop/multi-process apps.
- `:memory:` databases cannot use WAL (they report `memory`). Tests using `Data Source=:memory:` must not assert WAL; the file-backed test fixture is the one that validates WAL.
- WAL uses shared memory, so it does not work on read-only media or restricted sandboxes without `-shm` support.

### 28.7 Durability tradeoff to document

`synchronous=NORMAL` under WAL is the standard recommendation: no corruption risk, but the last committed transaction(s) not yet checkpointed may be lost on OS crash/power loss. Use `synchronous=FULL` when commits must be durable against power loss (slower). Expose as `SqliteRepositoryOptions.Synchronous` with `Normal` as default. `synchronous=OFF` is never acceptable for this framework.

### 28.8 Provider implementation items (derived)

- [ ] Add `RequireWal`, `Synchronous`, `BusyTimeoutMs`, `WalAutoCheckpointPages`, `JournalSizeLimitBytes`, `ConfigureConnection` to `SqliteRepositoryOptions`.
- [ ] Centralize connection open/config in a `SqliteConnectionFactory` so PRAGMAs are applied consistently regardless of pooling.
- [ ] `SqliteTransactionWrapper.Start()` uses immediate transactions.
- [ ] Add BUSY/LOCKED retry policy with backoff; exclude BUSY_SNAPSHOT from blind retry.
- [ ] `Rebuild()` → `wal_checkpoint(TRUNCATE)` + `VACUUM`; dispose → checkpoint + `ClearPool`.
- [ ] Tests: assert `journal_mode = wal` for file DBs; assert WAL is *not* required for `:memory:`; writer/writer contention test asserting retry; long-reader WAL-growth test documenting the checkpoint-starvation behavior; `EXPLAIN QUERY PLAN` unaffected by WAL.
- [ ] Document network-filesystem fallback and read-only directory requirement in README.

### 28.9 What we do *not* need

- No manual `PRAGMA journal_mode` per connection (it is persisted once).
- No `PRAGMA foreign_keys` (no foreign keys by design).
- No `locking_mode=EXCLUSIVE` (breaks multi-process; WAL already reduces lock traffic).
- No separate WAL tuning for reads; WAL gives snapshot reads by default.
