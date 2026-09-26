# SQLite Provider — Implementation Plan Set

This folder contains a phased, copy-paste-ready implementation plan for `GoLive.Saturn.Data.Sqlite`. It is written for an LLM with **no prior context** on this codebase. Give the implementing LLM exactly one phase file at a time, plus this README.

Reference design document (read for rationale, not required): `docs/sqlite-json-provider-proposal.md`.

## How to use this plan

1. Give the LLM this README first.
2. Give it `phase-0-scaffolding.md`. Run the exit criteria. Do not proceed until green.
3. Repeat for each phase in order. **Do not skip phases.** Each phase assumes the previous phase exists.
4. After each phase, run the stated verification command. If it fails, stop and fix before continuing.

## Repository facts the LLM must know

- Root: `D:\Work\Saturn.Data`
- Solution file: `D:\Work\Saturn.Data\Saturn.Data.slnx`
- Target framework for all provider projects: `net10.0`.
- Existing providers to mirror:
  - `D:\Work\Saturn.Data\Saturn.Data.MongoDb\Saturn.Data.MongoDb\` (namespace `Saturn.Data.MongoDb`)
  - `D:\Work\Saturn.Data\Saturn.Data.LiteDbX\Saturn.Data.LiteDbX\` (namespace `Saturn.Data.LiteDbX`)
  - `D:\Work\Saturn.Data\Saturn.Data.Stellar\Saturn.Data.Stellar\` (namespace `Saturn.Data.Stellar`) — best template for the write-behavior pipeline.
- Shared contracts (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Abstractions\GoLive.Saturn.Data.Abstractions\`
- Entities (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Entities\GoLive.Saturn.Data.Entities\`
- Shared contract tests (do not modify): `D:\Work\Saturn.Data\Saturn.Data.Testing.Shared\`
- Entities JSON converters (reuse): `D:\Work\Saturn.Data\Saturn.Data.Entities\Saturn.Data.Entities.JsonConverters\`

## Non-negotiable rules for the implementing LLM

1. **Do not modify** anything under `Saturn.Data.Abstractions`, `Saturn.Data.Entities`, `Saturn.Data.Testing.Shared`, or any existing provider. All work is additive.
2. **Never build SQL by concatenating values.** All values are `SqliteParameter`. Only identifiers (table/index names) may be interpolated, and only after passing through `QuoteIdentifier`.
3. **Follow the house code style** (from `AGENTS.md`):
   - File-scoped namespaces.
   - No comments of any kind in `.cs` files.
   - No XML doc comments.
   - No `_` prefix on private fields or locals; private fields are `lowerCamelCase`.
   - Fully cuddled Egyptian braces (`else` on the closing brace line).
   - One statement per line.
   - Use modern C#: pattern matching, switch expressions, `var`, target-typed `new()`.
   - Do not inject `IServiceProvider`; use explicit constructor dependencies.
4. **Do not invent signatures.** Copy member signatures from the interface files named in each phase.
5. If a build error occurs, fix only the error. Do not refactor unrelated code.
6. Run `dotnet build` after every file group, and the phase's test command at the end.

## Common commands

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj" --filter "FullyQualifiedName~BasicTests"
```

## Project layout the plan produces

```
D:\Work\Saturn.Data\Saturn.Data.Sqlite\
├─ Saturn.Data.Sqlite\
│  ├─ Saturn.Data.Sqlite.csproj
│  ├─ SqliteRepository.cs
│  ├─ SqliteRepository.ReadonlyRepository.cs
│  ├─ SqliteRepository.Repository.cs
│  ├─ SqliteRepository.Scoped.cs
│  ├─ SqliteRepository.ScopedReadonly.cs
│  ├─ SqliteRepository.SecondScoped.cs
│  ├─ SqliteRepository.SecondScopedReadonly.cs
│  ├─ SqliteRepository.WeakScoped.cs
│  ├─ SqliteRepository.WeakScopedReadonly.cs
│  ├─ SqliteRepository.WeakSecondScoped.cs
│  ├─ SqliteRepository.WeakSecondScopedReadonly.cs
│  ├─ SqliteRepository.TransparentScoped.cs
│  ├─ SqliteRepository.TransparentScopedReadonly.cs
│  ├─ SqliteRepository.Cascade.cs
│  ├─ SqliteRepositoryOptions.cs
│  ├─ SqliteConnectionFactory.cs
│  ├─ SqliteConnectionLease.cs
│  ├─ SqliteEntityStore.cs
│  ├─ SqliteDataUpdateDefinition.cs
│  ├─ SqliteTransactionWrapper.cs
│  ├─ ServicesExtensions.cs
│  ├─ Serialization\
│  │  ├─ EntityJsonSerializer.cs
│  │  ├─ EntityJsonSerializerOptions.cs
│  │  ├─ EntityJsonTypeInfoResolver.cs
│  │  ├─ RefJsonConverter.cs
│  │  ├─ WeakRefJsonConverter.cs
│  │  └─ PropertiesJsonConverter.cs
│  ├─ Query\
│  │  ├─ SqlFragment.cs
│  │  ├─ SqliteJsonPathResolver.cs
│  │  ├─ SqliteExpressionTranslator.cs
│  │  ├─ SqliteTranslationException.cs
│  │  ├─ SqliteQueryBuilder.cs
│  │  ├─ SqliteQueryable.cs
│  │  └─ SqliteQueryProvider.cs
│  ├─ Indexes\SqliteIndexManager.cs
│  ├─ Cascade\SqliteCascadeExecutor.cs
│  └─ ChangeFeed\
│     ├─ SqliteOutboxChangeFeedSink.cs
│     └─ ChangeFeedServicesExtensions.cs
└─ Saturn.Data.Sqlite.Tests\
   ├─ Saturn.Data.Sqlite.Tests.csproj
   ├─ UnitTestableSqliteRepository.cs
   ├─ DatabaseFixture.cs
   ├─ BasicTests.cs
   ├─ ScopedTests.cs
   ├─ ComprehensiveScopedTests.cs
   ├─ ScopedReadonlyContinuationTests.cs
   ├─ ChangeFeedTestFixture.cs
   ├─ ChangeFeedCollectionDefinition.cs
   ├─ ChangeFeedAfterWriteTests.cs
   ├─ SqliteChangeFeedPollerTests.cs
   ├─ CascadeTestFixture.cs
   ├─ CascadeTests.cs
   ├─ SqliteIndexManagerTests.cs
   ├─ ProviderSpecificTests.cs
   └─ Entities\
      ├─ BasicEntity.cs
      ├─ ChildEntity.cs
      └─ ParentScope.cs
```

## Phases

| Phase | File | Goal |
| --- | --- | --- |
| 0 | `phase-0-scaffolding.md` | Projects build, JSON1 probe passes |
| 1 | `phase-1-serialization-storage-crud.md` | Serialization, storage, core CRUD; `BasicRepositoryContractTests` green |
| 2 | `phase-2-query-translation.md` | Predicate→SQL translation, sorting, paging, `IQueryable` |
| 3 | `phase-3-scoped-variants.md` | All scoped interfaces; scoped contract tests green |
| 4 | `phase-4-mutations-indexes.md` | Patch, Increment, JsonUpdate, index manager |
| 5 | `phase-5-transactions-wal.md` | Real transactions, WAL, retry, concurrency |
| 6 | `phase-6-cascade-changefeed.md` | Cascade executor, change feed sink, DI, streaming |
| 7 | `phase-7-hardening-packaging.md` | Performance, README, packaging, full suite |
