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
| 3 | Sqlite adapter | in_progress |
| 4 | DocumentDb adapter | pending |
| 5 | Stellar decision + CLI | pending |
| 6 | Hardening + docs | pending |

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

