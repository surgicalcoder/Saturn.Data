# Phase 5 — Transactions, WAL, Concurrency

**Goal:** Real ACID transactions via `IDatabaseTransaction`, correct WAL usage, and a bounded retry policy for `SQLITE_BUSY` / `SQLITE_LOCKED`.

**Prerequisite:** Phase 4 complete and all tests green.

**Exit criteria:**
- `TransactionTests` pass: committed writes visible, rolled-back writes gone.
- `WalConcurrencyTests` pass: a second connection observes committed data; journal mode is `wal`.
- No deadlock when a write runs inside a transaction.

---

## Task 5.1 — Add retry options

Edit `SqliteRepositoryOptions.cs` and add:

```csharp
    public int BusyRetryCount { get; set; } = 5;

    public int BusyRetryBaseDelayMs { get; set; } = 25;
```

---

## Task 5.2 — Create `SqliteRetry.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteRetry.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal static class SqliteRetry
{
    private const int Busy = 5;
    private const int Locked = 6;
    private const int BusySnapshot = 261;

    public static async Task<int> ExecuteNonQueryWithRetryAsync(this SqliteCommand command, SqliteRepositoryOptions options, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsRetryable(exception) && attempt < options.BusyRetryCount)
            {
                await DelayAsync(attempt, options, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task<object> ExecuteScalarWithRetryAsync(this SqliteCommand command, SqliteRepositoryOptions options, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsRetryable(exception) && attempt < options.BusyRetryCount)
            {
                await DelayAsync(attempt, options, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryable(SqliteException exception)
    {
        if (exception.SqliteErrorCode != Busy && exception.SqliteErrorCode != Locked)
        {
            return false;
        }

        return exception.SqliteExtendedErrorCode != BusySnapshot;
    }

    private static Task DelayAsync(int attempt, SqliteRepositoryOptions options, CancellationToken cancellationToken)
    {
        var delay = options.BusyRetryBaseDelayMs * (int)Math.Pow(2, attempt);
        return Task.Delay(delay, cancellationToken);
    }
}
```

> `SqliteException.SqliteExtendedErrorCode` exists on `Microsoft.Data.Sqlite`. If a specific property name does not compile, use `exception.SqliteErrorCode` only and drop the `BusySnapshot` guard; document that a `BEGIN IMMEDIATE` transaction is what prevents 261.

---

## Task 5.3 — Create `SqliteTransactionWrapper.cs`

Path: `D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite\SqliteTransactionWrapper.cs`

```csharp
using GoLive.Saturn.Data.Abstractions;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public sealed class SqliteTransactionWrapper : IDatabaseTransaction
{
    private bool completed;
    private bool started;

    internal SqliteTransactionWrapper(SqliteConnection connection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction Transaction { get; private set; }

    public Task Start()
    {
        Transaction = Connection.BeginTransaction(deferred: false);
        started = true;
        return Task.CompletedTask;
    }

    public async Task CommitAsync()
    {
        if (!started)
        {
            throw new InvalidOperationException("Transaction has not been started.");
        }

        await Transaction.CommitAsync().ConfigureAwait(false);
        completed = true;
    }

    public async Task RollbackAsync()
    {
        if (!started || completed)
        {
            return;
        }

        await Transaction.RollbackAsync().ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (started && !completed)
            {
                await Transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        catch
        {
        }

        if (Transaction is not null)
        {
            await Transaction.DisposeAsync().ConfigureAwait(false);
        }

        await Connection.DisposeAsync().ConfigureAwait(false);
    }
}
```

> If `BeginTransaction(deferred: false)` does not compile on the pinned `Microsoft.Data.Sqlite` version, use `Connection.BeginTransaction()` instead. The immediate form is preferred because it avoids `SQLITE_BUSY_SNAPSHOT` on read-then-write transactions.

---

## Task 5.4 — Update `RentConnectionAsync` and implement `CreateTransaction`

### 5.4.1 Replace `RentConnectionAsync` in `SqliteRepository.cs`

```csharp
    protected async Task<SqliteConnectionLease> RentConnectionAsync(IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is SqliteTransactionWrapper wrapper)
        {
            return new SqliteConnectionLease(wrapper.Connection, ownsConnection: false);
        }

        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SqliteConnectionLease(connection, ownsConnection: true);
    }

    internal Task<SqliteConnectionLease> RentConnectionInternalAsync(IDatabaseTransaction transaction, CancellationToken cancellationToken)
        => RentConnectionAsync(transaction, cancellationToken);
```

### 5.4.2 Replace `CreateTransaction` in `SqliteRepository.Repository.cs`

```csharp
    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        await InitializeAsync(CancellationToken.None).ConfigureAwait(false);
        var connection = await connectionFactory.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        return new SqliteTransactionWrapper(connection);
    }
```

---

## Task 5.5 — Add the write gate

Add a field to `SqliteRepository.cs`:

```csharp
    private readonly SemaphoreSlim writeGate = new(1, 1);
```

Add a helper struct/class and method to `SqliteRepository.cs`:

```csharp
    private sealed class GateReleaser : IDisposable
    {
        private readonly SemaphoreSlim gate;

        public GateReleaser(SemaphoreSlim gate) => this.gate = gate;

        public void Dispose() => gate.Release();
    }

    private async Task<IDisposable> EnterWriteAsync(IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            return new GateReleaser(new SemaphoreSlim(0, 1));
        }

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new GateReleaser(writeGate);
    }
```

> The above hack (`new SemaphoreSlim(0,1)`) is wrong. Use a proper no-op disposable:
> ```csharp
>     private sealed class NoopDisposable : IDisposable
>     {
>         public static readonly NoopDisposable Instance = new();
>         public void Dispose() { }
>     }
>
>     private async Task<IDisposable> EnterWriteAsync(IDatabaseTransaction transaction, CancellationToken cancellationToken)
>     {
>         if (transaction is not null)
>         {
>             return NoopDisposable.Instance;
>         }
>
>         await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
>         return new GateReleaser(writeGate);
>     }
> ```
> Delete the `new SemaphoreSlim(0,1)` version; use `NoopDisposable`.

`Dispose()` must also dispose `writeGate`. Update `SqliteRepository.Dispose` to add `writeGate.Dispose();` before `SqliteConnection.ClearAllPools();`.

Then wrap the body of every write operation whose `transaction` may be `null` (`Insert` single/batch, `Save` single/batch, `Upsert` single/batch, `Update` all, `Delete` all, `HardDelete` all, `Restore` all, `Patch`, `JsonUpdate`, `Increment`) with:

```csharp
        using var writeScope = await EnterWriteAsync(transaction, cancellationToken).ConfigureAwait(false);
        try
        {
            ...existing body after context/behaviors...
        }
        catch (Exception exception)
        {
            await ApplyOnWriteFailed(context, exception).ConfigureAwait(false);
            throw;
        }
```

The `using var writeScope` must be declared inside the existing `try` block, immediately after `await InitializeAsync(...)`, so that the release happens in a `finally`. If the existing structure already has a `try/catch`, insert the `using` as the first statement of that `try`.

> If this is too invasive, a correct minimal alternative: do **not** add the gate at all. SQLite `busy_timeout` plus `SqliteRetry` already handle contention. Skipping the gate is acceptable for this phase. Choose the simplest path that compiles and passes tests.

---

## Task 5.6 — Apply the retry helper in write paths

For every write command execution in `SqliteRepository.Repository.cs`, `SqliteRepository.Patch.cs`, `SqliteRepository.Increment.cs`, replace:

```csharp
await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
```

with:

```csharp
await command.ExecuteNonQueryWithRetryAsync(sqliteOptions, cancellationToken).ConfigureAwait(false);
```

Do this only for write commands (`INSERT`, `UPDATE`, `DELETE`). Leave read commands unchanged.

---

## Task 5.7 — Add `RebuildAsync`

Add to `SqliteRepository.cs`:

```csharp
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
```

---

## Task 5.8 — Tests

### 5.8.1 `TransactionTests.cs`

```csharp
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class TransactionTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Commit_Persists_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-commit" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.CommitAsync();

        Assert.NotNull(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Rollback_Discards_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-rollback" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);
        await transaction.RollbackAsync();

        Assert.Null(await fixture.Repository.ById<BasicEntity>(entity.Id));
    }

    [Fact]
    public async Task Read_Inside_Transaction_Sees_Uncommitted_Write()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "tx-read" };

        await using var transaction = await fixture.Repository.CreateTransaction();
        await transaction.Start();
        await fixture.Repository.Insert(entity, transaction);

        var visible = await fixture.Repository.ById<BasicEntity>(entity.Id, transaction);
        Assert.NotNull(visible);

        await transaction.RollbackAsync();
    }
}
```

### 5.8.2 `WalConcurrencyTests.cs`

Add to `UnitTestableSqliteRepository`:

```csharp
    public async Task<List<string>> ReadAllNamesOnFreshConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await ConnectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT _doc FROM \"BasicEntity\";";

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
```

```csharp
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Testing.Shared.Entities;

namespace Saturn.Data.Sqlite.Tests;

public class WalConcurrencyTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await fixture.Repository.HardDelete<BasicEntity>(entity => true);
    }

    [Fact]
    public async Task Committed_Writes_Are_Visible_On_A_Second_Connection()
    {
        var entity = new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "wal-visible" };
        await fixture.Repository.Insert(entity);

        var documents = await fixture.Repository.ReadAllNamesOnFreshConnectionAsync();
        Assert.Contains(documents, document => document.Contains(entity.Id));
    }

    [Fact]
    public async Task Rebuild_Completes()
    {
        await fixture.Repository.Insert(new BasicEntity { Id = EntityIdGenerator.GenerateNewId(), Name = "rebuild" });
        await fixture.Repository.RebuildAsync();
        Assert.True(true);
    }

    [Fact]
    public async Task Journal_Mode_Is_Wal()
    {
        Assert.Equal("wal", await fixture.Repository.ReadJournalModeAsync());
    }
}
```

---

## Task 5.9 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet test "D:\Work\Saturn.Data\Saturn.Data.Sqlite\Saturn.Data.Sqlite.Tests\Saturn.Data.Sqlite.Tests.csproj"
```

---

## Do NOT

- Do not start a transaction inside another transaction; nested transactions are not implemented (SQLite has no true nested transactions). `AllowNestedTransactions` is reserved and unused.
- Do not acquire the write gate inside a transactional write (deadlock).
- Do not retry `SQLITE_BUSY_SNAPSHOT` (261) blindly; the immediate transaction mode prevents it.
- Do not change `journal_mode` per operation; it is set once at initialization.
- Do not add comments to `.cs` files.
