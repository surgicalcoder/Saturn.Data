using GoLive.Saturn.Data.Abstractions;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public sealed class DocumentDbTransaction : IDatabaseTransaction
{
    private readonly IDocumentStore store;
    private readonly bool supportsExplicitTransaction;
    private IDocumentSession session;
    private bool started;
    private bool completed;

    internal DocumentDbTransaction(IDocumentStore store, bool supportsExplicitTransaction)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.supportsExplicitTransaction = supportsExplicitTransaction;
    }

    internal IDocumentSession Session => session ?? throw new InvalidOperationException("Transaction has not been started.");

    public async Task Start()
    {
        session = store.OpenSession();

        if (supportsExplicitTransaction)
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
