# Phase 5 — Transactions and Concurrency

**Goal:** Implement `IDatabaseTransaction` over `Shiny.DocumentDb`'s unit of work, route writes through it when present, and translate the library's concurrency exception into Saturn's.

**Prerequisite:** Phase 4 complete; mutation tests green.

**Exit criteria:**
- `DocumentDbTransaction` implements `IDatabaseTransaction` (`Start`, `CommitAsync`, `RollbackAsync`, `DisposeAsync`).
- Writes issued with a transaction are buffered and only persisted on commit; a rollback persists nothing.
- `CreateTransaction()` throws `NotSupportedException` when `Capabilities.SupportsTransactions` is false.
- `TransactionTests` green on SQLite (they early-return on backends without transaction support).
- `Shiny.DocumentDb.ConcurrencyException` is translated to `FailedToUpdateException` wherever it can surface.

**Read this before coding.** Shiny.DocumentDb's `IDocumentSession` is a **write buffer**, not a live transaction: writes are queued and applied on `SaveChanges()`, and — per the library's documentation — **reads do not see buffered writes**. Saturn's SQLite provider asserts the opposite (`Read_Inside_Transaction_Sees_Uncommitted_Write`). Do not try to force the SQLite semantics; instead make the behaviour explicit and adjust the provider-specific test to assert the real contract. This is a documented divergence, not a bug.

---

## Task 5.1 — Create `DocumentDbTransaction.cs`

`...\GoLive.Saturn.Data.DocumentDb\DocumentDbTransaction.cs`:

```csharp
using GoLive.Saturn.Data.Abstractions;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbTransaction : IDatabaseTransaction
{
    private IDocumentSession session;
    private bool started;
    private bool completed;

    internal DocumentDbTransaction(IDocumentStore store, bool supportsExplicitTransaction)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        SupportsExplicitTransaction = supportsExplicitTransaction;
    }

    internal IDocumentStore Store { get; }

    internal bool SupportsExplicitTransaction { get; }

    internal IDocumentSession Session => session ?? throw new InvalidOperationException("Transaction has not been started.");

    public async Task Start()
    {
        session = Store.OpenSession();

        if (SupportsExplicitTransaction)
        {
            await session.BeginTransaction().ConfigureAwait(false);
        }

        started = true;
    }

    public async Task CommitAsync()
    {
        if (!started)
        {
            throw new InvalidOperationException("Transaction has not been started.");
        }

        await session.SaveChanges().ConfigureAwait(false);
        completed = true;
    }

    public Task RollbackAsync()
    {
        completed = true;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        completed = true;
    }
}
```

> Adjust `OpenSession()`/`BeginTransaction()` return shapes to the real API (Phase 0 probe). If `BeginTransaction` is unsupported on a backend, `SupportsExplicitTransaction` must be false for it — derive it from `Capabilities.BackendName` in `CreateTransaction`.

---

## Task 5.2 — Route writes through the transaction

Add these helpers to `DocumentDbRepository.cs`:

```csharp
    protected async Task InsertWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Add(entity);
            return;
        }

        await Store.Insert(entity).ConfigureAwait(false);
    }

    protected async Task InsertManyWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IReadOnlyList<TItem> entities, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.AddRange(entities);
            return;
        }

        await Store.BatchInsert(entities).ConfigureAwait(false);
    }

    protected async Task UpsertWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Upsert(entity);
            return;
        }

        await Store.Upsert(entity).ConfigureAwait(false);
    }

    protected async Task UpsertManyWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IReadOnlyList<TItem> entities, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.UpsertRange(entities);
            return;
        }

        await Store.BatchUpsert(entities).ConfigureAwait(false);
    }

    protected async Task UpdateWithTransactionAsync<TItem>(IDatabaseTransaction transaction, TItem entity, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            documentDbTransaction.Session.Update(entity);
            return;
        }

        await Store.Update(entity).ConfigureAwait(false);
    }

    protected async Task RemoveWithTransactionAsync<TItem>(IDatabaseTransaction transaction, IEnumerable<string> ids, CancellationToken cancellationToken) where TItem : Entity
    {
        if (transaction is DocumentDbTransaction documentDbTransaction)
        {
            foreach (var id in ids)
            {
                documentDbTransaction.Session.Remove<TItem>(id);
            }

            return;
        }

        await Store.BatchRemove<TItem>(ids).ConfigureAwait(false);
    }

    protected static Exception TranslateLibraryException(Exception exception)
        => exception is Shiny.DocumentDb.ConcurrencyException
            ? new FailedToUpdateException()
            : exception;
```

Then **replace every direct `Store.Insert` / `Store.BatchInsert` / `Store.Upsert` / `Store.BatchUpsert` / `Store.Update` / `Store.BatchRemove` call in the write paths** (`DocumentDbRepository.Repository.cs`, `DocumentDbRepository.Patch.cs`, `DocumentDbRepository.Increment.cs`) with the corresponding helper, passing the method's `transaction` argument. In `Patch` and `Increment`, wrap the `Store.Update(entity)` call in a try/catch that rethrows `TranslateLibraryException(exception)`.

> `Session.AddRange` / `Session.UpsertRange` / `Session.Remove<T>` names come from the library's unit-of-work docs. If the real names differ, fix them here; the helper indirection means only these six methods change.

---

## Task 5.3 — Implement `CreateTransaction()`

Replace the Phase 1 stub in `DocumentDbRepository.Repository.cs`:

```csharp
    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        await InitializeAsync(CancellationToken.None).ConfigureAwait(false);

        if (!capabilities.SupportsTransactions)
        {
            throw new NotSupportedException($"Shiny.DocumentDb backend '{capabilities.BackendName}' does not support transactions.");
        }

        var supportsExplicit = !capabilities.RequiresSingleConnection;
        var transaction = new DocumentDbTransaction(Store, supportsExplicit);
        await transaction.Start().ConfigureAwait(false);

        return transaction;
    }
```

> `RequiresSingleConnection` is used here as a proxy for "explicit `BeginTransaction` is unavailable" (SQLite/DuckDB). If the probe shows explicit transactions work there too, set `supportsExplicit` to `true` unconditionally. Phase 7 validates this per backend.

---

## Task 5.4 — Probe explicit-transaction read visibility

Add to `ProviderApiTests` (or a new `TransactionProbeTests`):

```csharp
    [Fact]
    public async Task Explicit_Transaction_Read_Visibility()
    {
        using var store = CreateStore();
        await store.Insert(new Probe { Id = HexId, Name = "seed" });

        var visibleInsideTransaction = false;

        try
        {
            await using var session = store.OpenSession();
            await session.BeginTransaction();
            session.Update(new Probe { Id = HexId, Name = "changed" });
            var readBack = await store.Get<Probe>(HexId);
            visibleInsideTransaction = readBack?.Name == "changed";
        }
        catch (Exception exception)
        {
            output.WriteLine($"Explicit transaction probe exception: {exception.GetType().Name}: {exception.Message}");
        }

        output.WriteLine($"Uncommitted writes visible to reads on this backend: {visibleInsideTransaction}");
    }
```

Feed the result into `TransactionTests` (Task 5.5) and the README.

---

## Task 5.5 — Tests

Port `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\TransactionTests.cs` to `...\TransactionTests.cs` (namespace and repository types changed), then:

1. Add a guard at the top of every test: `if (!fixture.Repository.Capabilities.SupportsTransactions) { return; }` — SQLite/DuckDB support it; Cosmos/NoSQL backends do not.
2. **Rewrite `Read_Inside_Transaction_Sees_Uncommitted_Write`** to assert the *actual* behaviour recorded by Task 5.4:
   - if uncommitted writes are visible → keep the SQLite assertion;
   - if not → rename it `Read_Inside_Transaction_Does_Not_See_Buffered_Writes` and assert the write becomes visible only after `CommitAsync()`.
3. Keep `Commit_Persists_Write`, `Rollback_Discards_Write`, `Update_Inside_Transaction_Is_Visible_And_Rolled_Back`.

Also expose the capability on the fixture for the change-feed contract tests (Phase 6 needs it):

```csharp
    public bool SupportsTransactions => Repository.Capabilities.SupportsTransactions;
```

Add that property to `DatabaseFixture` only if it is required by an interface it implements; otherwise expose it as a plain property used by the tests.

---

## Task 5.6 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "...\GoLive.Saturn.Data.DocumentDb.Tests.csproj"
```

---

## Do NOT

- Do not attempt to make reads see uncommitted writes; record and assert the real behaviour instead.
- Do not add independent connections or a second store for transactions — the session comes from the store.
- Do not add a write gate; SQLite/DuckDB already serialize inside the library.
- Do not implement cascade or change feed here.
- Do not add comments to `.cs` files.
