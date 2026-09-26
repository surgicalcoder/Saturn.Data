using GoLive.Saturn.Data.Abstractions;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

public sealed class SqliteTransactionWrapper : IDatabaseTransaction
{
    private bool started;
    private bool completed;

    internal SqliteTransactionWrapper(SqliteConnection connection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction? Transaction { get; private set; }

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

        await Transaction!.CommitAsync().ConfigureAwait(false);
        completed = true;
    }

    public async Task RollbackAsync()
    {
        if (!started || completed)
        {
            return;
        }

        await Transaction!.RollbackAsync().ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (started && !completed)
            {
                await Transaction!.RollbackAsync().ConfigureAwait(false);
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
