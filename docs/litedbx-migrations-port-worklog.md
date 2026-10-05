# LiteDbX.Migrations → Saturn Port — Worklog

Companion to `docs/litedbx-migrations-port-proposal.md`. This file is the resume point: it records what is done, what is in flight, exact file paths, and open decisions. Update it after every phase, before committing.

## Environment

- Repo: `D:\Work\Saturn.Data`, branch `master`.
- .NET: `net10.0` across providers, `Nullable=enable`, `LangVersion=latest`.
- Build: `dotnet build Saturn.Data.slnx` (solution uses `.slnx`).
- Tests: `dotnet test <project>`.
- Commit policy for this effort: one commit at the end of each phase, **no push**.

## Phase tracker

| Phase | Scope | Status |
| --- | --- | --- |
| 0 | F1/F2 provider fixes + core `GoLive.Saturn.Data.Migrations` extraction | done |
| 1 | LiteDbX adapter + parity | done |
| 2 | MongoDb adapter | done |
| 3 | Sqlite adapter | done |
| 4 | DocumentDb adapter | done (in-place only) |
| 5 | Stellar decision + CLI | done (Stellar 3a; CLI deferred) |
| 6 | Hardening + docs | done (docs; backlog recorded) |

## Resume notes

- Proposal committed at `2242f61`, file `docs/litedbx-migrations-port-proposal.md`.
- Option C chosen: one engine over an abstract mutable document tree + per-provider `IMigrationStore` adapters.
- Reference engine source: `D:\Work\LiteDBX\LiteDbX.Migrations\` (15 files, ~4.4k LoC). NuGet XML for LiteDbX at `C:\Users\Monty\.nuget\packages\litedbx\2.0.0\lib\net10.0\LiteDB.xml`.

## Phase 0 log

### Decisions taken
- Core assembly name: `Saturn.Data.Migrations` (folder `Saturn.Data.Migrations\Saturn.Data.Migrations\`), namespace `GoLive.Saturn.Data.Migrations`, references only `Saturn.Data.Entities` + `Saturn.Data.Abstractions`. No provider packages.
- Value model is **materialized** (concrete `MigrationValue`/`MigrationObject`/`MigrationArray`) rather than an abstract tree. Adapters convert native docs ⇄ this model. This keeps `DocumentPathNavigator` a direct port (concrete factories) and avoids threading a document factory through the engine. See proposal §5.1/§5: the "materializes one" branch of Option C.
- Store abstraction: `IMigrationStore`, `IMigrationCollection`, `MigrationStoreCapabilities`, `MigrationIndexDefinition`.
- Entry point: `IMigrationStoreSource.CreateMigrationStore()` + `MigrationStoreExtensions.Migrations(this IMigrationStore)`.
- `MigrationObjectId` reuses `GoLive.Saturn.Data.Entities.Entity.TryParseId` for normalization and `EntityIdGenerator.GenerateNewId()` for generation.

### Work items
- [x] F1 LiteDbX soft-delete flip (`LiteDbRepository.cs`) — `Phase0PersistenceTests`.
- [x] F2 LiteDbX `Properties`→`_p` (`EntityMapper.cs`) + `DontSerializeEmptyCollections = true`; omit empty. Full LiteDbX suite 59/59.
- [x] Core project + value model
- [x] `DocumentPathNavigator`
- [x] `MigrationPredicates`
- [x] Operations + builders
- [x] `MigrationRunner` (in-place + rebuild/swap + journal + dry-run + remap)
- [x] Reporting/options/progress
- [x] In-memory store + 11 tests green
- [ ] Deferred: `RepairReference`, `InsertDocumentWhen`, backup cleanup API, duplicate-target-id detection, strict-path failure reporting, index replay

### Gotchas discovered while implementing
- `Entity.TryParseId` accepts **both** 24-char hex **and** 16-char base64url ids. A 16-char test string like `"not-an-object-id"` is therefore a *valid* id and won't trigger `GenerateNewId`; use a clearly invalid string (e.g. `"bad-id"`) in tests.
- LiteDbX `BsonMapper` custom member serializers bypass `SerializeNullValues`; returning `BsonValue.Null` still writes `_p: null`. Solution: do not custom-serialize `Properties`; map the member to `_p` via `ResolveMember` and rely on default dictionary serialization plus `DontSerializeEmptyCollections = true`.
- `BsonMapper.Entity<T>()` method shadows the `Entity` type inside `CustomEntityMapper`; use `nameof(GoLive.Saturn.Data.Entities.Entity.Properties)` fully qualified.
- Rebuild path requires `IMigrationStore.Capabilities.SupportsRebuild` and `SupportsRenameCollection`; the runner throws a clear `NotSupportedException` otherwise.

### Next
- Phase 1: `LiteDbxMigrationStore` in `Saturn.Data.LiteDbX` + `IMigrationStoreSource` on `LiteDbRepository`, run the LunarFlare migrations against real LiteDbX data, parity test vs `LiteDbX.Migrations`.

## Phase 1 log

### Delivered
- `Saturn.Data.LiteDbX/Migrations/LiteDbxBsonConverter.cs` — `LiteDbX.BsonDocument`/`BsonValue` ⇄ `MigrationObject`/`MigrationValue`.
- `Saturn.Data.LiteDbX/Migrations/LiteDbxMigrationStore.cs` — `LiteDbxMigrationStore` + `LiteDbxMigrationCollection`.
- `LiteDbRepository` now implements `IMigrationStoreSource`; `CreateMigrationStore()` returns a store over the existing `database` handle.
- `Phase1MigrationTests.Migrations_ConvertLegacyStringIdsOnRealStorage` runs the LunarFlare-style migration on real LiteDbX: string `_id`/`Scope` → ObjectId, empty `Properties` and null `Payload` removed, rebuild + swap + journal all verified. Full LTE suite 60/60.

### Gotchas
- LiteDbX is async-first: `GetCollectionNames` returns `IAsyncEnumerable<string>`; `RenameCollection`/`DropCollection`/`Update`/`Delete` return `ValueTask<bool>`; `FindAll` returns `IAsyncEnumerable<BsonDocument>`. The adapter awaits these.
- `ILiteCollection.Insert(BsonDocument, CancellationToken)` is async; `BsonAutoId` has no `String` member — insert a document that already carries a string `_id` regardless of the collection's auto-id, and LiteDbX honours it.
- `IMigrationStore.CollectionExists` stayed synchronous; the LiteDbX adapter bridges the `ValueTask<bool>` with `AsTask().GetAwaiter().GetResult()` (single lightweight call during the journal check).

### Next
- Phase 2: `MongoMigrationStore` in `Saturn.Data.MongoDb` + `IMigrationStoreSource` on `MongoDbRepository`; native `_id` ObjectId, `_p`/`_v` aliases, driver rename/drop, index enumeration.

## Phase 2 log

### Delivered
- `Saturn.Data.MongoDb/Migrations/MongoBsonConverter.cs` — `MongoDB.Bson` ⇄ `MigrationObject` (ObjectId, Decimal128, BsonBinary/Guid, MinKey/MaxKey).
- `Saturn.Data.MongoDb/Migrations/MongoMigrationStore.cs` — `MongoMigrationStore` + `MongoMigrationCollection` (ListCollectionNames, RenameCollectionAsync, DropCollectionAsync, cursor-based scan with `IsDeleted != true` filter, index enumeration via `Indexes.List`).
- `MongoDbRepository` implements `IMigrationStoreSource`.
- `Phase2MigrationTests` on real MongoDB passes; full suite 107/107.

### Gotchas
- Mongo `BsonValue` binary is `BsonBinaryData` (not `byte[]`); converter uses `AsByteArray`; Guid is UuidStandard binary and mapped via `AsGuid`.
- `BsonType.Decimal128` ⇄ `decimal`.
- Include-deleted scan uses `Filter.Ne("IsDeleted", true)` so missing flags are visible (matches F1 semantics).
- Field aliasing (`_p`/`_v`) not applied; definitions use physical names for now.

### Next
- Phase 3: `SqliteMigrationStore` in `Saturn.Data.Sqlite`; JSON `_doc` ⇄ `MigrationObject` via the provider's `EntityJsonSerializer`, projection-column maintenance, `PRAGMA index_list`, `ALTER TABLE` swap, `knownTables` invalidation.

## Phase 3 log

### Delivered
- `Saturn.Data.Sqlite/Migrations/SqliteJsonConverter.cs` — `_doc` JSON ⇄ `MigrationObject` (PascalCase, refs/ids as strings, `_id` canonically from the `_id` column, `Id` written on output).
- `Saturn.Data.Sqlite/Migrations/SqliteMigrationStore.cs` — table enumeration via `sqlite_master`, `INSERT ... ON CONFLICT(_id) DO UPDATE` upsert with recomputed `_v`/`_deleted`/`_scope`/`_scope2`/`_archived`, `ALTER TABLE ... RENAME`, canonical index drop/recreate, `knownTables` invalidation.
- `SqliteRepository` partial implements `IMigrationStoreSource`.
- `Phase3MigrationTests` passes; full suite 84/84.

### Gotchas
- SQLite `RenameCollection` does not rename indexes; the adapter drops `ix_{src}__*` and creates `ix_{dst}__*` so canonical provider indexes stay consistent.
- JSON ids remain strings; `ConvertId`/`ConvertField` ObjectId kinds are serialized back to 24-hex strings (`SupportsObjectIdOnDisk=false`).
- Seeding legacy docs is easiest via the store itself (`GetCollection().InsertAsync`) — it writes canonical `Id` + projections.

### Next
- Phase 4: `DocumentDbMigrationStore` for the Shiny shared `documents` table (logical collection = `TypeName`), discriminator re-tag rebuild.

## Phase 4 log

### Delivered
- `Saturn.Data.DocumentDb/Migrations/DocumentDbJsonConverter.cs` and `DocumentDbMigrationStore.cs` over the Shiny raw lane (`store.Collection(name, "Id")`; scan via `QueryStream("1=1", null)`).
- `DocumentDbRepository` implements `IMigrationStoreSource`. Explicit selectors now resolve without enumeration (core runner change).
- Existing DocumentDb smoke suite green.

### Limitations / deferred
- `SupportsRebuild=false` / `SupportsRenameCollection=false`: `ConvertId` (rebuild) migrations throw a clear `NotSupportedException` on DocumentDb. The proposal's discriminator re-tag rebuild is not implemented.
- No automated raw migration test: `QueryStream` did not complete in the test host (hung >5 min). The adapter compiles but raw behavior is unverified. Revisit with the Shiny `IJsonDocumentQuery.ToCursorPage` pagination or a direct `DatabaseProvider` SQL scan.

### Next
- Phase 5: Stellar — explicit unsupported `IMigrationStoreSource` (tier 3a) with actionable error; optional CLI.

## Phase 5 log

### Delivered
- `StellarRepository : IMigrationStoreSource`; `CreateMigrationStore()` throws an actionable `NotSupportedException` (tier 3a).
- `Phase5MigrationTests` asserts the message.

### Deferred
- Provider-agnostic CLI migrator (arg parsing + provider factory + repository construction). The Appendix C Saturn `Program.cs` remains the reference implementation.

### Next
- Phase 6: authoring/DI polish, docs, and the deferred engine operations (`RepairReference`, backup cleanup, duplicate-id detection, index replay, alias map).

## Phase 6 log

### Delivered
- Progress tracker in the proposal (§17) and this worklog; whole-solution build clean (`dotnet build Saturn.Data.slnx`).
- Backlog recorded in the proposal (§17 Phase 6 checklist).

### Backlog (not done)
- Engine operations: `RepairReference`, `InsertDocumentWhen`, backup cleanup (`CleanupBackupsAsync`/`KeepLatestCount`), duplicate-target-id detection, strict-path failure reporting, index replay on rebuild.
- Field-name alias map (`_p`/`_v`) for BSON⇄JSON parity.
- DocumentDb discriminator re-tag rebuild + raw test.
- Provider-agnostic CLI.

### How to resume
1. Read the proposal §17 and this file.
2. `dotnet build Saturn.Data.slnx`.
3. Run tests: core (`Saturn.Data.Migrations.Tests`), LiteDbX, MongoDb (needs localhost:27017), Sqlite.
4. Pick a backlog item; add an adapter method only when an engine path needs it; keep the core provider-free.

## Post-phase — blockers and deferred items resolved

### Blocker 1 — DocumentDb raw hang (fixed)
Root cause: `ScanAsync` used `IJsonDocumentCollection.QueryStream`, holding a live cursor while the loop wrote (`Update`/shadow `Insert`) — a read-during-write deadlock on the SQLite provider. Also, `RawDocument`'s first two ctor args are `(id, typeName)`, not `(typeName, id)` (an early version mis-ordered them and re-tagged rows with the id as the type).

Fix: scan via `IDocumentBackup.ExportAsync` into a `MemoryStream` (a **snapshot**, cursor closed), parse the v1 array `{ id, docType, data }`, and write back with `BulkImportAsync`/`BatchRemove`. Logical collection rename = export source rows → bulk import under the target `TypeName` → `BatchRemove` the source ids. This gives DocumentDb a real shadow/swap rebuild through the generic engine. `SupportsRebuild`/`SupportsRenameCollection` are true when the store implements `IDocumentBackup`. `Phase4MigrationTests` passes (rebuild + cleanup). DocumentDb full suite 128/128; no hang.

### Blocker 2 — field aliases (fixed)
- Core: `IMigrationFieldMap` (+ `IdentityMigrationFieldMap`, `MigrationFieldMap`), `MigrationFieldTranslation` (top-level, recursive-safe via collected moves), `IMigrationStore.FieldMap` (default identity). Runner translates physical→logical on scan and logical→physical before insert/update.
- LiteDbX maps `Properties`→`_p`; Mongo maps `Properties`→`_p` and `Version`→`_v`; Sqlite/DocumentDb/InMemory identity.
- Tests: `EngineFeaturesTests.FieldTranslation_RoundTripsAliases`, `Runner_AppliesFieldMapOnScan`.

### Deferred items now done
- `RepairReference` (builder `.RepairReference(path).FromCollection(...).FromMigration(...).WhenReferenceCollectionIs(...).Apply()`), backed by `RemapLookup` loaded from `__saturn_migration_id_mappings` and updated live as ids are generated.
- `InsertDocumentWhen` (collected into `context.PendingInserts`, deduped, inserted after the collection pass / into the shadow).
- `CleanupBackupsAsync(BackupCleanupOptions)` + `BackupCleanupReport`.
- Duplicate-target-id detection during rebuild (`DuplicateTargetIdSample`, `RebuildValidationSummary.DuplicateTargetIdCount`).
- Index replay on rebuild; Sqlite `GetIndexes` now reads columns via `PRAGMA index_info`.
- DocumentDb rebuild + automated test.
- CLI: `Saturn.Data.Migrations.Cli` + `IMigrationModule`.

### Backlog still open
- None blocking. Remaining polish:
  - DocumentDb swap is crash-safe but not fully atomic (import is `SingleTransaction`; the source delete runs after). A true one-transaction swap needs `IDocumentSession` access, which the raw lane doesn't expose.
  - DocumentDb index enumeration is best-effort (no public list API; only `CreateIndex`).
  - Alias maps are centralized (`MigrationFieldAliases`) and conformance-tested, not introspected from provider conventions at runtime (providers don't expose that publicly).

### Follow-up round (strict-path, LiteDbX indexes, Mongo conformance, DocumentDb batch)
- Strict path: `MigrationRunOptions.StrictPathResolution` / `ThrowOnStrictPathFailure`, `WithStrictPathResolution()`; `DocumentMigrationExecutionContext.StrictPathFailures`; `DocumentPathNavigator.ResolveFailure`; counts surface on `CollectionMigrationResult`/`MigrationExecutionResult`. Tests `StrictPathResolution_*`.
- LiteDbX `GetIndexes()` reads `$indexes` (`collection`/`name`/`expression`/`unique`, skips `_id`); `EnsureIndexAsync` replays via `BsonExpression.Create`. Capability `SupportsIndexEnumeration=true`. Test `IndexEnumeration_ReadsSystemIndexes`.
- Mongo conformance test `FieldAliases_MatchMongoStorageShape` proves `_p`/`_v`. LiteDbX conformance `FieldAliases_MatchLiteDbxStorageShape`.
- DocumentDb: `SupportsBatchInsert` + `IMigrationCollection.InsertManyAsync` (default loops; DocumentDb uses `BulkRestoreOptions { SingleTransaction = true }`); runner batches shadow inserts when supported.
- Field aliases centralized in core `MigrationFieldAliases` (LiteDbx/Mongo); adapters reference them.

### Final verification
- Whole solution builds (`dotnet build Saturn.Data.slnx`).
- Tests: core 19, LiteDbX 62, MongoDb 108, Sqlite 84, DocumentDb 127, Stellar Phase5 1.




