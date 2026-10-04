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
- Value model names: `MigrationValue`, `MigrationObject`, `MigrationArray`, `MigrationValueKind`, `MigrationObjectId`.
- Store abstraction: `IMigrationStore`, `IMigrationCollection`, `MigrationStoreCapabilities`, `MigrationIndexDefinition`.
- Entry point: `IMigrationStoreSource.CreateMigrationStore()` in Abstractions + `MigrationStoreExtensions.Migrations(this IMigrationStore)`.

### Work items
- [ ] F1 LiteDbX soft-delete flip (`LiteDbRepository.cs`)
- [ ] F2 LiteDbX `Properties`→`_p` (`EntityMapper.cs`)
- [ ] Core project + value model
- [ ] `DocumentPathNavigator` port
- [ ] `MigrationPredicates` port
- [ ] Operations + builders port
- [ ] `MigrationRunner` port
- [ ] Reporting/options/progress port
- [ ] In-memory store test double + tests
- [ ] Build + tests green

### Notes / gotchas discovered while implementing
- (append here)
