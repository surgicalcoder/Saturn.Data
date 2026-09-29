# Phase 7 — Hardening, Multi-Backend Matrix and Packaging

**Goal:** Validate the capability table against real backends, add the DuckDB CI gate, document the provider's limitations, and produce the package.

**Prerequisite:** Phase 6 complete; the full SQLite suite green.

**Exit criteria:**
- `dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Release` succeeds.
- SQLite and DuckDB both run the full contract suite (Tier 1 gates).
- `DocumentDbCapabilities` and the README capability table agree with observed behaviour on every backend you can run.
- `GoLive.Saturn.Data.DocumentDb.7.0.0.nupkg` is produced.
- `README.md` documents the provider and its limitations.

---

## Task 7.1 — Add the DuckDB gate

`...\Saturn.Data.DocumentDb.Tests\DuckDbDatabaseFixture.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Shiny.DocumentDb;
using Shiny.DocumentDb.DuckDb;

namespace Saturn.Data.DocumentDb.Tests;

public class DuckDbDatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public DuckDbDatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-{Guid.NewGuid():N}.duckdb");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(DuckDbDatabaseProvider),
                ConfigureStore = options => options.DatabaseProvider = new DuckDbDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}
```

Then add DuckDB subclasses of the shared suites using distinct class names so xUnit does not collide with the SQLite ones:

```csharp
public class DuckDbBasicTests(DuckDbDatabaseFixture fixture)
    : BasicRepositoryContractTests<DuckDbDatabaseFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<DuckDbDatabaseFixture>;
```

Repeat for: `ScopedRepositoryContractTests`, `ComprehensiveScopedRepositoryContractTests`, `CascadeContractTests`, `ChangeFeedContractTests` (+ fixture), `ChangeFeedPollerTests`, `ChangeSetPatchContractTests`, and `BasicRepositoryContractTests`. Name them `DuckDb*Tests`.

> Only add a DuckDB subclass where the suite is meaningful. `ChangeFeedContractTests` requires `SupportsTransactions`; DuckDB reports `true` through `RequiresSingleConnection` handling — verify, and if it does not, skip the DuckDB change-feed subclass and note why.

---

## Task 7.2 — Validate the capability table

For every backend you can run (Tier 1: SQLite, DuckDB; Tier 2: PostgreSQL, MySQL, MariaDB, SQL Server via containers), run `ProviderApiTests` against it and record the actual results. Then correct `BackendFeatureTable.cs` so each row matches observed behaviour.

Add container-backed fixtures only if a job can run them; keep them out of the default test run by using a distinct test class name and a `[Trait("Backend", "Container")]` attribute so the default `dotnet test` can exclude them with `--filter "Backend!=Container"`.

`...\PostgreSqlDatabaseFixture.cs` (only if containers are available):

```csharp
public class PostgreSqlDatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public PostgreSqlDatabaseFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("SATURN_PG")
            ?? throw new InvalidOperationException("SATURN_PG is not set.");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(PostgreSqlDatabaseProvider),
                ConfigureStore = options => options.DatabaseProvider = new PostgreSqlDatabaseProvider(connectionString)
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}
```

---

## Task 7.3 — Optional: atomic `Patch` fast path

Only do this if `ProviderApiTests` proved `ExecuteUpdate` works on the target backend. In `DocumentDbRepository.Patch.cs`, when **all** of the following hold, use `ExecuteUpdate` instead of read-modify-write:

- `documentDbOptions.PatchStrategy == PatchStrategy.ExecuteUpdate`
- `capabilities.SupportsExecuteUpdate`
- the patch contains only `$set` (no `$unset`, no `$inc`)
- `expectedVersion` is null

The call shape is:

```csharp
await Store.Query<TItem>()
    .Where(item => item.Id == normalized)
    .ExecuteUpdate(builder => builder.Set(item => item.Count, 5))
    .ConfigureAwait(false);
```

Because the property list is dynamic, this requires building the `Action<IDocumentUpdateBuilder<TItem>>` expression with reflection. If that proves fragile, **do not implement it** — leave the read-modify-write path and record in the README that `Patch` is a version-checked read-modify-write rather than a single atomic statement. Correctness beats the optimization.

---

## Task 7.4 — Optional: streaming reads

`All`, `Many`, `Random` and `ById` currently materialise into a `List<T>` before wrapping (`AsyncEnumerableFactory.From`). If `Query<T>().ToAsyncEnumerable()` is available (Phase 0 probe), replace the materialising paths in `DocumentDbRepository.ReadonlyRepository.cs` with streaming enumeration. Keep the materialising path for `One`/`Count`/`First`. If streaming changes ordering or paging semantics on any backend, keep materialising and document it.

---

## Task 7.5 — Optional: AOT escape hatch

The provider is reflection-based (README design rule 3). If a consumer needs AOT, they pass `DocumentDbRepositoryOptions.JsonSerializerContext`, which Phase 1 already combines into the resolver. Verify that path works by adding one test that constructs the repository with a `JsonSerializerContext` for the test entities and runs a round-trip with `DocumentDbRepositoryOptions.UseReflectionFallback = false`. If the round-trip fails, record the failure in the README as a known limitation rather than papering over it.

---

## Task 7.6 — Update the repository README

Add to `D:\Work\Saturn.Data\README.md`:

1. The provider in the runtime libraries list and the packages table: `Saturn.Data.DocumentDb/GoLive.Saturn.Data.DocumentDb/GoLive.Saturn.Data.DocumentDb.csproj` → `GoLive.Saturn.Data.DocumentDb`, described as "Shiny.DocumentDb-backed provider — one implementation, every backend Shiny.DocumentDb supports".
2. A "Supported backends" line naming them, with the guidance:
   - Use this provider for PostgreSQL, SQL Server, MySQL/MariaDB, Oracle, CockroachDB, DuckDB, Cosmos DB, Redis, RavenDB, Firestore, Azure Table, DynamoDB, Amazon DocumentDB, IndexedDB, SQLCipher.
   - Use the native providers for MongoDB and LiteDB.
   - **SQLite is served by this provider until `GoLive.Saturn.Data.Sqlite` ships**, then switch.
3. The test commands, including the SQLite and DuckDB fixtures and the container exclude filter.
4. A **Limitations** section stating, in plain language:
   - The provider uses reflection-based serialization and is **not AOT/trim-safe** by default; an AOT consumer must supply `JsonSerializerContext`.
   - Unique and sparse indexes, and TTL (`ExpireAfter`), require per-type configuration and are reported through `OnUnsupportedIndexOption` rather than created.
   - The raw-JSON lane (`Collection(...)`) exists only on relational backends; `JsonUpdate` falls back to a typed write elsewhere.
   - Transactions exist on relational and LiteDB backends; Cosmos and the key-partitioned stores compensate rather than commit, and `CreateTransaction` throws where unsupported.
   - Predicates the backend cannot translate throw by default; set `UnsupportedPredicateBehaviour.FallbackToClient` to evaluate them in memory (document the cost).
   - `Random` is emulated with a count plus a random offset (two round-trips).
   - Azure Table caps a document near 64 KB; DynamoDB caps an item at 400 KB.
   - Scope membership over `MultiscopedEntity.Scopes` is unsupported on MariaDB.

---

## Task 7.7 — Packaging and publish wiring

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj" -c Release
dotnet pack "D:\Work\Saturn.Data\Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb\GoLive.Saturn.Data.DocumentDb.csproj" -c Release
```

Confirm:

- The library has `GeneratePackageOnBuild=true` and therefore appears in the publish pipeline's packable set (`GeneratePackageOnBuild == true`).
- The test project is `IsPackable=false`.
- Both projects are in `Saturn.Data.slnx` under `/DocumentDb/`, so `scripts/detect-changed-projects.ps1` (or its `dotnet-affected` replacement) discovers them by directory scan.

---

## Task 7.8 — Final acceptance checklist

- [ ] `dotnet build Saturn.Data.slnx -c Release` succeeds.
- [ ] SQLite and DuckDB run the full contract suite; container backends run when configured.
- [ ] No files under `Saturn.Data.Abstractions`, `Saturn.Data.Entities`, `Saturn.Data.Testing.Shared`, `Saturn.Data.ChangeTracking`, or any other provider were modified.
- [ ] `DocumentDbRepository` implements `IReadonlyRepository`, `IRepository`, all ten scoped interfaces, `IRepositoryIndexManager`.
- [ ] `DocumentDbRepositoryOptions` contains no option that does nothing.
- [ ] `DocumentDbCapabilities`/`BackendFeatureTable` match observed behaviour for every backend tested.
- [ ] Id is the 24-hex string on every backend; `continueFrom` is an `Id`-ordered range query.
- [ ] No `ConfigureDocument` call is required for the provider to function.
- [ ] No comments exist in any `.cs` file under `Saturn.Data.DocumentDb`.
- [ ] README updated with backends, limitations and test commands.
- [ ] Package packs.

---

## Do NOT

- Do not mark this phase complete with failing or silently skipped tests — every skip must be capability-gated and explained.
- Do not enable optional features by default.
- Do not add per-type configuration to make a test pass.
- Do not use Shiny.DocumentDb tenancy, spatial, vector, full-text, temporal, blob, outbox or Orleans features.
- Do not add comments to `.cs` files.
