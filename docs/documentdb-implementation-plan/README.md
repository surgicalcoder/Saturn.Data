# DocumentDb Provider — Implementation Plan Set

Phased, copy-paste-ready implementation plan for `GoLive.Saturn.Data.DocumentDb`, written for an LLM with **no prior context** on this codebase. Give the implementing LLM this README plus exactly one phase file at a time.

**Design source of truth:** [../documentdb-provider-proposal.md](../documentdb-provider-proposal.md). Read it for rationale. This folder contains the actionable steps.

## How to use this plan

1. Give the LLM this README first, then `phase-0-scaffolding-and-api-verification.md`.
2. Run the phase's verification. **Do not proceed until it passes.**
3. Repeat in order 0 → 7. Each phase assumes the previous one exists.
4. If any statement in this plan contradicts the actual `Shiny.DocumentDb` package, **trust the package** and update the phase file. Phase 0 exists to catch exactly that.

## The single most important instruction

**Mirror the SQLite provider.** `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\` is a complete, working, committed provider that implements the exact same abstraction surface this plan targets. Its file layout, partial-class split, write-behaviour pipeline, predicate helpers and test fixtures are the template. Your job is to replace its storage/query layer (`Microsoft.Data.Sqlite` + hand-written SQL) with `Shiny.DocumentDb` calls, and to change the cross-cutting semantics as described in each phase.

Read these SQLite files before writing anything:

| Purpose | File |
| --- | --- |
| Constructor, options, DDL, write pipeline | `Saturn.Data.Sqlite\SqliteRepository.cs` |
| Unscoped reads | `Saturn.Data.Sqlite\SqliteRepository.ReadonlyRepository.cs` |
| Unscoped writes | `Saturn.Data.Sqlite\SqliteRepository.Repository.cs` |
| Scoped variants (10 files) | `Saturn.Data.Sqlite\SqliteRepository.Scoped*.cs` |
| Shared scoped cores | `Saturn.Data.Sqlite\SqliteRepository.ScopedOperations.cs` |
| Patch / Increment | `Saturn.Data.Sqlite\SqliteRepository.Patch.cs`, `SqliteRepository.Increment.cs` |
| Cascade | `Saturn.Data.Sqlite\SqliteRepository.Cascade.cs`, `Cascade\*` |
| Change feed | `Saturn.Data.Sqlite\ChangeFeed\*` |
| Serialization | `Saturn.Data.Sqlite\Serialization\*` |
| Query fallback pattern | `Saturn.Data.Sqlite\Query\*` |
| Tests | `Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\*` |

## Repository facts

- Root: `D:\Work\Saturn.Data` · Solution: `D:\Work\Saturn.Data\Saturn.Data.slnx`
- Abstractions (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\`
- Entities (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Entities\GoLive.Saturn.Data.Entities\`
- JSON converters (reuse): `D:\Work\Saturn.Data\Saturn.Data.Entities\Saturn.Data.Entities.JsonConverters\`
- Shared contract tests (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Testing.Shared\`
- New project root: `D:\Work\Saturn.Data\Saturn.Data.DocumentDb\`
- Target framework: `net10.0` (matches both this repo and Shiny.DocumentDb)

## Non-negotiable rules

1. **Do not modify** anything under `Saturn.Data.Abstractions`, `Saturn.Data.Entities`, `Saturn.Data.Testing.Shared`, `Saturn.Data.ChangeTracking`, or any existing provider. Additive only.
2. **Never build a query by string concatenation of values.** Predicates are `Expression<Func<T,bool>>`; the raw-SQL escape hatch takes parameters.
3. House code style (from `AGENTS.md`): file-scoped namespaces; **no comments in `.cs` files**; no XML doc comments; no `_`-prefixed fields (private fields are `lowerCamelCase`); cuddled Egyptian braces; one statement per line; modern C# (`var`, pattern matching, switch expressions, target-typed `new()`).
4. **No `IServiceProvider` injection.** Explicit dependencies in constructors.
5. **`ProjectReference` for all intra-repo dependencies, never NuGet.** `Shiny.DocumentDb` and its backend packages are third-party → NuGet.
6. Do not invent API shapes. Every `Shiny.DocumentDb` call must either appear in this plan or be proven by a compile in Phase 0.
7. Run `dotnet build` after each file group and the phase's test command at the end.

## Verified Shiny.DocumentDb facts (from its readme + bundled skill guide)

The implementing LLM may rely on the following. Anything not listed here must be verified in Phase 0.

| Concern | Fact |
| --- | --- |
| Core package | `Shiny.DocumentDb` (abstractions, `DocumentStore`, `IDocumentStore`, fluent query, expression visitor, DI, telemetry) |
| Backend packages | `.Sqlite`, `.Sqlite.SqlCipher`, `.MySql`, `.MariaDb`, `.SqlServer`, `.PostgreSql`, `.CockroachDb`, `.Oracle`, `.LiteDb`, `.CosmosDb`, `.MongoDb`, `.DocumentDb` (Amazon), `.Redis`, `.RavenDb`, `.Firestore`, `.AzureTable`, `.DynamoDb`, `.DuckDb`, `.IndexedDb` |
| Target | `net10.0` |
| Build a store | `new DocumentStore(new DocumentStoreOptions { DatabaseProvider = new SqliteDatabaseProvider("Data Source=x.db") })`; or `services.AddDocumentStore(opts => ...)` |
| Options | `DatabaseProvider` (required), `TableName` (default `"documents"`), `TypeNameResolution` (default `ShortName`), `JsonSerializerOptions`, `UseReflectionFallback` (default `true`), `Logging`, `TenantIdAccessor`; per-type `ConfigureDocument<T>(cfg => ...)` |
| Storage shape | One JSON body per document + envelope columns; **one table per store by default, discriminated by `TypeName`**; `cfg.Table` gives a per-type table but is exclusive to one type |
| Id | Mandatory public `Id` property (`Guid`/`int`/`long`/`string`), auto-generated when default and written back; our `Entity.Id` (string) satisfies it |
| Reads | `store.Get<T>(id)`; `store.Query<T>().Where(λ).OrderBy(λ).Paginate(skip,take).ToList()/First()/FirstOrDefault()/Count()`; `.Select(...)`; `.WhereIn(λ, values)`; `.ToQueryString()`; `.ToAsyncEnumerable()`; `.PageResult(page,size)`; `.ToCursorPage(cursor,take)` |
| Writes | `store.Insert(doc)`, `store.Update(doc)`, `store.Update(doc, patch: true)`, `store.Upsert(doc)`, `store.Upsert(doc, patchIfUpdate: false)`, `store.Remove<T>(id)` → bool, `store.Clear<T>()` → int, `store.SetProperty<T>(id, λ, value)`, `store.RemoveProperty<T>(id, λ)`, `store.BatchInsert(list)`, `store.BatchUpsert(list)`, `store.BatchUpdate(list)`, `store.BatchRemove<T>(ids)` |
| Query-level writes | `Query<T>().Where(λ).ExecuteUpdate(b => b.Set(λ, value))`, `Query<T>().Where(λ).ExecuteDelete()` |
| Transactions | `store.OpenSession()` → `IDocumentSession` (write buffer + `SaveChanges`); explicit `session.BeginTransaction()` (relational) with `IsolationLevel` and `session.Get(id, LockMode.Update)` |
| Concurrency | `cfg.MapVersionProperty(x => x.Version)` → version in the body, 1 on insert, checked+incremented on update, `ConcurrencyException` on conflict (every provider) |
| Soft delete | `cfg.AddSoftDelete(x => x.IsDeleted)` + `IncludeDeleted()`/`OnlyDeleted()`/`Restore`/`PurgeDeleted`/`HardDelete` |
| Indexes | `store.CreateIndexAsync<T>(λ, jsonTypeInfo)` — non-unique, runtime; `cfg.MapUniqueIndex(λ)` — unique, **per-type config** |
| Change feeds | `IObservableDocumentStore.NotifyOnChange<T>()` (in-process); `IChangeFeedDocumentStore.SubscribeChanges<T>` (PostgreSQL/SQL Server/Cosmos/DynamoDB) |
| Capability probes | `SupportsTransactions`, `SupportsSpatial`, `SupportsVector`, `SupportsFullText`, `SupportsRawJson`, `SupportsPessimisticLocking`, `MaxBlobSize`; optional interfaces via `is`: `ITemporalDocumentStore`, `IObservableDocumentStore`, `IChangeFeedDocumentStore`, `IDocumentBackup`, `IDocumentMaintenance` |
| JSON collections (raw JSON) | `store.Collection(nameOrType)` → `IJsonDocumentCollection` with `Insert/Update/Upsert/Get/Remove/BatchRemove/Clear/CreateIndex/Query` — **relational providers only**, unavailable inside a session |
| Connection model | Server SQL opens a connection per operation and pools; **SQLite and DuckDB use one long-lived connection + serialization** (`RequiresSingleConnection`) |
| Non-default merge modes | `Update(patch:true)` / `Upsert(patchIfUpdate:false)` are **relational-only**; other backends throw `NotSupportedException` |

## Decisions this plan implements (from proposal §13 / §6)

| # | Decision |
| --- | --- |
| 1 | Package `GoLive.Saturn.Data.DocumentDb` |
| 2 | CI gates: **SQLite + DuckDB** (server-free). LiteDB and MongoDB are local/opt-in only |
| 3 | Per-type registration is **optional**; the default provider must work with no `ConfigureDocument` at all |
| 4 | Change feed = **outbox with CAS** (like the other providers), not the library's native change feed |
| 5 | Untranslatable/unsupported predicates: **throw** by default (`UnsupportedPredicateBehaviour.Throw`), optional in-memory fallback |
| 6 | `Patch` is **atomic** via `ExecuteUpdate` where supported |
| 7 | **Never** touch Shiny.DocumentDb tenancy; Saturn scopes stay plain data (`ScopeId`, `SecondScopeId`, `Scopes`) |
| 8 | SQLite is included as a CI gate; LiteDB/Mongo are not |
| 9 | Id stays the **24-hex string**; never convert to `Guid`; `continueFrom` is `Id > token` |

## Global provider design rules

1. **No per-type configuration for core semantics.** Soft delete, version checks and collection separation are implemented by *us* with predicates and field writes, so a consumer can point the provider at any entity type without declaring it. `ConfigureDocument<T>` is exposed only as an optional advanced hook.
2. **Capability gate everything backend-specific.** Catch `NotSupportedException` from the library and either rethrow with an actionable message (default) or fall back to in-memory evaluation (`UnsupportedPredicateBehaviour.FallbackToClient`).
3. **Reflection mode is expected.** Build `JsonSerializerOptions` with a combined resolver and leave `UseReflectionFallback = true`. Document that the provider is **not AOT-safe**.
4. **Identity is a string.** Always assign `Entity.Id` ourselves before writing; never rely on the library's generation.
5. **Scopes are data.** Filter on `ScopeId`/`SecondScopeId` (string) — never `Ref<T>` equality — and gate `Scopes.Contains`.
6. **One `IDocumentStore` per database.** Do not add an external write gate; SQLite/DuckDB already serialize internally.

## Phase map

| Phase | File | Delivers |
| --- | --- | --- |
| 0 | `phase-0-scaffolding-and-api-verification.md` | Projects, slnx, options, capability probe, **API probe test**, smoke test |
| 1 | `phase-1-serialization-storage-crud.md` | Serialization contract, unscoped CRUD, soft delete, write pipeline; `BasicRepositoryContractTests` green |
| 2 | `phase-2-query-paging-continuation.md` | Predicate delegation, sorting, paging, `continueFrom`, fallback, `IQueryable` |
| 3 | `phase-3-scoped-variants.md` | All 10 scoped interfaces; scoped contract tests green |
| 4 | `phase-4-mutations-indexes.md` | `Patch`, `JsonUpdate`, `Increment`, `EnsureIndexes` |
| 5 | `phase-5-transactions-concurrency.md` | `IDatabaseTransaction`, capability gating, CAS failures |
| 6 | `phase-6-cascade-changefeed-di.md` | Cascade executor, outbox change feed, DI |
| 7 | `phase-7-hardening-packaging.md` | Capability matrix validation, multi-backend matrix, README, pack |

## Commands

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.Tests\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj" --filter "FullyQualifiedName~BasicTests"
```

## Terminology

- **Store** — `IDocumentStore`, the Shiny.DocumentDb instance.
- **Backend** — the `IDatabaseProvider` implementation (SQLite, DuckDB, PostgreSQL…).
- **Capability** — a backend feature probed at runtime (`SupportsTransactions` etc.).
- **Fallback** — evaluating a predicate in memory because the backend cannot translate it.
- **Docs** — the `_doc`-equivalent JSON body Shiny.DocumentDb stores; we never touch it directly except through the JSON collection lane.

---

## Discoveries from implementing Phases 0 and 1 (authoritative — trust these over §"Verified facts")

Shiny.DocumentDb was inspected at **version 14.0.0** (`net10.0`). The following correct and extend the facts table above.

### API shape corrections

| Assumption in the plan | Reality |
| --- | --- |
| `DocumentStoreOptions` accepts a `ConfigureStore` action that sets `DatabaseProvider` | `DatabaseProvider` is a **`required`** member, so it must be set in the object initializer. `DocumentDbRepositoryOptions` therefore exposes a `DatabaseProvider` property; `ConfigureStore` is only for optional tweaks. |
| `options.ConfigureDocument<T>(...)` | Does **not exist**. Per-type configuration lives on `DocumentStoreOptions.Mappings` (`DocumentMappingRegistry`): `MapVersionProperty<T>`, `AddQueryFilter<T>`, `AddUniqueIndex`, `MapIdProperty<T>`, `MapSpatialProperty<T>`, `MapTemporal<T>`, etc. This validates the decision to avoid per-type config for core semantics. |
| `store.Query<T>()` returns something `IQueryable<T>`-compatible | It returns `IDocumentQuery<T>`, which is **not** `IQueryable<T>`. Do not assign it to `IQueryable<T>`. |
| Capability probes | `IDocumentStore` exposes `SupportsTransactions`, `SupportsPessimisticLocking`, `SupportsSpatial`, `SupportsVector`, `SupportsFullText`, `MaxBlobSize`. There is **no `SupportsRawJson`**. `IDatabaseProvider` exposes `RequiresSingleConnection`, `SupportsUniqueIndexes`, `SupportsJsonMergePatch`, `SupportsBatchUpsert`, `SupportsBulkCopy`, `SupportsBulkReplace`, `SupportsChangeFeed`, `SupportsTemporal`, `SupportsComputedColumns`, `SupportsSoundex`, `SupportsUserFunctions`, `UpsertsConflictsOnAnyUniqueKey`, `MaxBlobSize`. |
| `IDocumentStore.CreateIndexAsync` | Not on `IDocumentStore`. Index creation lives elsewhere (Phase 4 must locate it). Non-unique JSON indexes are created through a different surface; unique indexes are per-type config. |
| `IDocumentSession.Remove<T>(id)` / `UpsertRange` | `Remove` is **non-generic** (`Remove(object id)`). `AddRange<T>` exists; there is **no `UpsertRange<T>`** — loop `Upsert` per item. `IDocumentSession` implements `IAsyncDisposable`. |
| `ISoftDeletable.DeletedAt` used with `DateTimeOffset` | It is `DateTime?` (and `DeletedBy`/`DeletedArchivedBy` are `string`). Use `DateTime.UtcNow`. |

### The critical translation finding

**`PredicateHelper.And` (from `GoLive.Saturn.Data.Abstractions`) produces bitwise `Expression.And`, which Shiny.DocumentDb's expression lowerer rejects** with `NotSupportedException: Expression '... And ...' is not supported in a query predicate`. It requires `AndAlso`.

Consequences for every later phase:

- Never compose query predicates with `.And(...)`. Use `Saturn.Data.DocumentDb.Query.PredicateComposer.AndAlso(left, right)`, which rebuilds the tree with `Expression.AndAlso` and rebinds the parameter.
- This applies to the soft-delete filter (Phase 1, done), scope predicates (Phase 3), continuation (Phase 2), and cascade predicates (Phase 6).

### SQLite backend probe results (Phase 0)

```
SupportsTransactions=True            SupportsPessimisticLocking=False
SupportsSpatial=True                 SupportsVector=False
SupportsFullText=True                MaxBlobSize=1073741824
RequiresSingleConnection=True        SupportsUniqueIndexes=True
SupportsJsonMergePatch=True          SupportsBatchUpsert=True
SupportsChangeFeed=False             SupportsTemporal=True
```

`SqliteDatabaseProvider.SupportsJsonMergePatch/SupportsChangeFeed` are **explicit interface implementations** — read them through an `IDatabaseProvider` reference, not the concrete type.

### `IDocumentQuery<T>` surface (from the package XML docs)

```
Where(Expression<Func<T,bool>>)                      OrderBy(Expression<Func<T,object>>)
OrderByDescending(Expression<Func<T,object>>)        Paginate(int skip, int take)
ToList(ct)  First(ct)  FirstOrDefault(ct)            Single/ SingleOrDefault / Any / Count(ct)
Sum/Min/Max/Average(selector, ct)                    ExecuteUpdate(Action<IDocumentUpdateBuilder<T>>, ct)
ExecuteUpdate(Expression<Func<T,object>>, object, ct)  ExecuteDelete(ct)
Select<TOut>(selector, JsonTypeInfo<TOut>)           Project(string, JsonTypeInfo<T>)
GroupBy(...) / Join(...)                             IgnoreQueryFilters(...)
ToCursorPage(cursor, take)                           ToAsyncEnumerable(ct)
ToQueryString()                                      RawJsonRows(take?, ct)   SupportsRawJson
```

**`SortOrder<T>.Field` is `Expression<Func<T, object>>`, which matches `OrderBy`/`OrderByDescending` exactly — pass it straight through.** `ExecuteUpdate(selector, value)` is the per-field atomic patch primitive for Phase 4. There is **no `Where(string)` on the typed query** and **no `WhereIn`** — use `Where(x => captured.Contains(x.Id))`, which is confirmed to translate.

### Second critical translation finding: no string comparison

`string.CompareTo`, `string.CompareOrdinal` and `string.Compare` are **all rejected** as query values (`NotSupportedException`). There is therefore **no server-side way to express `Id > token`** in a compiled predicate.

Consequence for `continueFrom`: Phase 2 implements it as *server-side predicate narrowing plus an in-memory keyset bound* —

- the caller's predicate and the soft-delete filter run server-side (so the candidate set is narrowed),
- the ordering is applied server-side,
- the `Id > token` bound and the page size are applied in memory.

This is correct but not O(log n) for continuation pages. The library's own keyset paging (`ToCursorPage`) is opaque-cursor based and cannot be driven by our Id token. If continuation over very large sets becomes a problem, the options are: expose an opt-in `ToCursorPage` mode with an opaque cursor on a new API, or drop `continueFrom` in favour of it. Recorded as a known limitation in Phase 7's README task.

### Index creation and mutation strategy (Phases 4)

- `CreateIndexAsync<T>(JsonTypeInfo<T>, IEnumerable<Expression<Func<T, object>>>)` lives on the **concrete `DocumentStore`**, not on `IDocumentStore`. `EnsureIndexes` resolves it by reflection on the runtime store type, supplies the `JsonTypeInfo<T>` from `Serializer.JsonOptions.GetTypeInfo(typeof(T))`, and reports through `OnUnsupportedIndexOption` when it is unavailable. Unique and sparse indexes, and `ExpireAfter`, are reported unsupported (unique is per-type config via `Mappings`).
- `Patch` and `Increment` are **version-checked read-modify-write** (`Get` → validate `Version` → mutate → `Update` with an incremented `Version`), which is correct on every backend. The atomic fast path exists — `IDocumentQuery<T>.ExecuteUpdate(Expression<Func<T,object>>, object, ct)` sets one field server-side — and is the Phase 7 optimization for `$set`-only patches with no version check.

### Transactions (Phase 5)

- `IDatabaseTransaction` maps to `IDocumentStore.OpenSession()`; `CommitAsync` calls `SaveChanges()`, `RollbackAsync` simply discards the session (nothing is written until `SaveChanges`).
- **`IDocumentSession` is a write buffer, and reads do not see buffered writes.** Confirmed on SQLite: after `Insert(entity, transaction)` a plain `ById` returns `null` until `CommitAsync`. The Phase 5 test asserts this deliberately (`Buffered_Write_Is_Not_Visible_Until_Commit`) rather than the SQLite provider's live-transaction semantics.
- Session method shapes: `Add<T>(T)`, `AddRange<T>(IEnumerable<T>)`, `Update<T>(T)`, `Upsert<T>(T)`, **`Remove<T>(object id)` (generic is required — `Remove(id)` alone will not infer)**. There is no session-level batch upsert/remove; loop.
- All write paths route through `InsertWithTransactionAsync` / `InsertManyWithTransactionAsync` / `UpsertWithTransactionAsync` / `UpsertManyWithTransactionAsync` / `UpdateWithTransactionAsync` / `RemoveWithTransactionAsync`, so a supplied transaction is always honoured.
- `CreateTransaction()` throws when `Capabilities.SupportsTransactions` is false. `!RequiresSingleConnection` is used as the proxy for whether an explicit `BeginTransaction` should be issued. `ConcurrencyException` handling is deferred: it can only surface if a consumer opts into per-type `MapVersionProperty`, which this provider deliberately does not do.

### Cascade and change feed (Phase 6)

- Cascade reuses the attribute-based relation resolver unchanged; the child lookup is a server-side predicate (`ScopeId == parentId`, else `Scopes.Contains`, else `Id == parentId`), and the apply step is read-modify-write per child (or `BatchRemove` for hard delete).
- The outbox sink stores rows in **non-entity internal types** (`ChangeFeedCounterRow`, `ChangeFeedOutboxRow` with `Id`, `Seq`, `Source`, `PayloadJson`). The library's generic document APIs require `T : class` (not `Entity`); the transaction write helpers were relaxed to `where TItem : class` to accommodate them. The row `Id` is the zero-padded sequence (`D20`), and the whole `ChangeFeedRecord` is serialized into `PayloadJson` so no per-field column mapping is needed.
- The counter is **not CAS-guarded** (that would need per-type `MapVersionProperty`). Sequence gaps are possible under concurrency; a duplicate allocation would collide on the padded `Id` primary key. Acceptable for v1, documented here; tightening it is a Phase 7 candidate.

### Confirmed working API surface (used and passing in Phases 0–1)

```csharp
await store.Insert(entity);                                  // Task
await store.Get<T>(id);                                      // Task<T>
await store.Update(entity);                                  // Task (full replace)
await store.Update(entity, patch: true);                     // Task (merge)
await store.Upsert(entity);                                  // Task (merge-or-insert)
await store.Remove<T>(id);                                   // Task<bool>
await store.BatchInsert(list);                               // Task<int>
await store.BatchRemove<T>(ids);                             // Task<int>
await store.BatchUpsert(list);                               // Task<int>
await store.BatchUpdate(list);                               // Task<int>
await store.SetProperty<T>(id, x => x.Name, value);          // Task
await store.RemoveProperty<T>(id, x => x.Name);              // Task

await store.Query<T>().Where(predicate).OrderBy(selector).Paginate(skip, take).ToList();
await store.Query<T>().Where(predicate).Count();
await store.Query<T>().Where(item => capturedIds.Contains(item.Id)).ToList();   // IN works

await using var session = store.OpenSession();
session.Add(entity);
session.AddRange(entities);
session.Update(entity);
session.Upsert(entity);
session.Remove(id);
await session.SaveChanges();
await session.BeginTransaction();
```

