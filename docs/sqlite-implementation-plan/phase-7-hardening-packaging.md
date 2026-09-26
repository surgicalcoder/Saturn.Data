# Phase 7 — Hardening, Optional Features, Packaging

**Goal:** Verify the full test matrix, add only the optional features that are justified, update docs, and produce the NuGet package.

**Prerequisite:** Phase 6 complete and all tests green.

**Exit criteria:**
- `dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Release` succeeds.
- `dotnet test` runs every SQLite test class green.
- `dotnet pack` produces `GoLive.Saturn.Data.Sqlite.7.0.0.nupkg`.
- `README.md` documents the provider.

**Rule:** Sections 7.1–7.5 are required. Sections 7.6–7.9 are optional. Do not start an optional item until 7.1–7.5 pass.

---

## 7.1 — Full test matrix (required)

Run all tests and confirm the following test classes exist and pass. If any class is missing, go back to the phase that creates it:

| Test class | Phase |
| --- | --- |
| `SmokeTests` | 0 |
| `BasicTests` | 1 |
| `ProviderSpecificTests` | 1 |
| `QueryTranslationTests` | 2 |
| `ContinuationTests` | 2 |
| `ScopedTests` | 3 |
| `ComprehensiveScopedTests` | 3 |
| `ScopedReadonlyContinuationTests` | 3 |
| `PatchWritesTests` | 4 |
| `IncrementWritesTests` | 4 |
| `SqliteIndexManagerTests` | 4 |
| `TransactionTests` | 5 |
| `WalConcurrencyTests` | 5 |
| `CascadeTests` | 6 |
| `ChangeFeedAfterWriteTests` | 6 |
| `SqliteChangeFeedPollerTests` | 6 |
| `ChangeFeedDiTests` | 6 |

```powershell
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj" --logger "console;verbosity=normal"
```

Also confirm the existing providers still build and pass (do not let the new project break them):

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Release
```

---

## 7.2 — Server-side projection (required if trivial, otherwise document as deferred)

Today the interface default projection methods (`All<TItem,TProjection>`, `ById<..>`, `Many<..>`, `One<..>`) materialize entities and apply the compiled selector. That is correct.

Implement `SqliteRepositoryOptions.ServerSideProjection` only if the selector is a simple member access (e.g. `item => item.Name`) or an anonymous object of member accesses. For an anonymous projection, build:

```sql
SELECT json_object('P0', json_extract(_doc,'$.A'), 'P1', json_extract(_doc,'$.B')) FROM ...
```

and deserialize into `TProjection`. If the selector is anything else, fall back to the in-memory default.

If this is not trivial, set `ServerSideProjection` to be ignored and add a `Warnings` note in the README. Do not destabilize the passing suite.

---

## 7.3 — Streaming `IAsyncEnumerable` (required if trivial, otherwise document as deferred)

Today `All`, `Many`, `Random`, and `ById` materialize a `List<T>` and wrap it with `AsyncEnumerableFactory.From`. This is correct but holds the whole result in memory.

Optional upgrade: replace `LoadListAsync` usage in `All`/`Many` with an async enumerator that owns a `SqliteConnectionLease` and a `SqliteDataReader`, yields items as it reads, and disposes both when enumeration ends. If implemented:

- The connection lease must be disposed in the enumerator's `finally`.
- Do not hold the read transaction open across `await Task.Delay` or user code (it can starve WAL checkpoints).
- Keep the materializing path as a fallback for `One`/`Count`/`Exists`.

If not implemented, add this to the README limitations: "Large queries materialize into memory."

---

## 7.4 — JSONB mode (required: verify it works or remove the option)

`SqliteRepositoryOptions.UseJsonB` exists but is unused. Either:

- **Implement:** store `_doc` as `BLOB` and write `jsonb(@doc)` / read `json(_doc)`. Update the DDL to `_doc BLOB NOT NULL`, and in every write `SqliteEntityStore`/repository method wrap the parameter with `jsonb(...)`. In reads, `json_extract` works on JSONB directly, but returning `_doc` for deserialization must use `json(_doc)`.
- **Or remove** the `UseJsonB` property and delete this task.

Do not leave a `UseJsonB` option that silently does nothing.

---

## 7.5 — README update (required)

Edit `D:\Work\Saturn.Data\README.md`. Add a SQLite row to the provider/package tables and a short section:

- Package: `GoLive.Saturn.Data.Sqlite`.
- Storage: JSON documents in SQLite with JSON1/JSONB.
- Registration:
  ```csharp
  services.AddSaturnSqliteRepositoryServices();
  services.AddSingleton(new SqliteRepositoryOptions { DataSource = "app.db" });
  ```
  (Adjust to match how the existing providers are documented.)
- Limitations:
  - `ExpireAfter` (TTL) index options are not supported.
  - `IQueryable` may evaluate in memory.
  - Nested transactions are not supported.
  - Whole-file encryption requires `Microsoft.Data.Sqlite.Core` + a SQLCipher bundle (see `docs/sqlite-json-provider-proposal.md` §27).
  - Network filesystems do not support WAL reliably.

---

## 7.6 — Index-usage assertions (optional)

Add a test that runs `EXPLAIN QUERY PLAN` on a scoped query and asserts the plan references the scope index. Example helper on `UnitTestableSqliteRepository`:

```csharp
    public async Task<string> ExplainAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql};";

        var builder = new System.Text.StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            builder.AppendLine(reader.GetString(3));
        }

        return builder.ToString();
    }
```

Assert `Assert.Contains("ix_", plan)` for a query of the form `SELECT _doc FROM "ChildEntity" WHERE _scope = '...' AND _deleted = 0`.

---

## 7.7 — SQLCipher (optional, only if required)

See `docs/sqlite-json-provider-proposal.md` §27.2. Steps:

1. Replace `Microsoft.Data.Sqlite` with `Microsoft.Data.Sqlite.Core` plus exactly one of:
   - `SQLitePCLRaw.bundle_e_sqlcipher` (free community), or
   - `SQLitePCLRaw.bundle_zetetic` (commercial).
2. Initialize `SQLitePCL.Batteries_V2.Init()` once at startup.
3. Set `EncryptionPassword` from a secret provider; never log it.
4. Add a test that creates an encrypted database and reads it back with the key, and that opening without the key fails.

Do not make SQLCipher the default; keep it opt-in.

---

## 7.8 — FTS5 (optional)

See `docs/sqlite-json-provider-proposal.md` §27.3. Add an `EnableFullTextSearch` path that:

1. Creates `<collection>_fts` with `content='<collection>', content_rowid='rowid'`.
2. Keeps it in sync inside the write transaction.
3. Exposes `SqliteRepository.SearchAsync<TItem>(string query, int take)` returning matches ordered by `bm25()`.

Do not attempt substring search via `MATCH`; the existing `string.Contains` → `instr` translation already covers that.

---

## 7.9 — Real `IQueryProvider` (optional)

Replace the eager `IQueryable<TItem>()` implementation with a deferred `SqliteQueryable<T>` + `SqliteQueryProvider` that recognizes `Where`, `OrderBy(Descending)`, `ThenBy(Descending)`, `Skip`, `Take`, `Select`, `First(OrDefault)`, `Single(OrDefault)`, `Count`, `Any`, translating recognized shapes through `SqliteQueryBuilder` and delegating everything else to `AsEnumerable()`.

Only do this if a consumer needs deferred `IQueryable`. No shared contract test requires it.

---

## 7.10 — Packaging

```powershell
dotnet pack "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj" -c Release
```

Confirm the `.nupkg` is produced and contains `GoLive.Saturn.Data.Sqlite.dll`.

Check `D:\Work\Saturn.Data\.github\workflows\` and `D:\Work\Saturn.Data\scripts\detect-changed-projects.ps1`. The new project must be discoverable by the changed-project detection. If the script has an explicit project list, add `Saturn.Data.Sqlite\Saturn.Data.Sqlite\Saturn.Data.Sqlite.csproj`. If it walks the solution, no change is needed.

---

## 7.11 — Final acceptance checklist

- [ ] `dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Release` succeeds.
- [ ] Every test class in 7.1 passes.
- [ ] No files under `Saturn.Data.Abstractions`, `Saturn.Data.Entities`, `Saturn.Data.Testing.Shared`, or the Mongo/LiteDbX/Stellar projects were modified.
- [ ] `SqliteRepository` implements:
  - `IReadonlyRepository`, `IRepository`
  - `IScopedRepository`, `IScopedReadonlyRepository`
  - `ISecondScopedRepository`, `ISecondScopedReadonlyRepository`
  - `IWeakScopedRepository`, `IWeakScopedReadonlyRepository`
  - `IWeakSecondScopedRepository`, `IWeakSecondScopedReadonlyRepository`
  - `ITransparentScopedRepository`, `ITransparentScopedReadonlyRepository`
  - `IRepositoryIndexManager`
- [ ] `SqliteRepositoryOptions` has no unused options (remove or implement `UseJsonB`, `ServerSideProjection`, `EnableFullTextSearch`, `UseJsonMergePatch`, `AllowNestedTransactions`).
- [ ] No comments exist in any `.cs` file under `Saturn.Data.Sqlite`.
- [ ] README updated.
- [ ] Package packs.

---

## Do NOT

- Do not mark this phase complete with failing or skipped tests.
- Do not enable optional features by default.
- Do not modify shared projects to make tests pass.
- Do not add comments to `.cs` files.
