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
| 0 | F1/F2 provider fixes + core `GoLive.Saturn.Data.Migrations` extraction | in_progress |
| 1 | LiteDbX adapter + parity (LunarFlare migrations) | pending |
| 2 | MongoDb adapter | pending |
| 3 | Sqlite adapter | pending |
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

