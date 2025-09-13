using GoLive.Saturn.Data.Abstractions;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public class MongoDbTransactionWrapper(IClientSessionHandle session)
    : IDatabaseTransaction
{
    internal readonly IClientSessionHandle Session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task Start()
    {
        Session.StartTransaction();
    }

    public async Task CommitAsync()
    {
        if (Session.IsInTransaction)
        {
            await Session.CommitTransactionAsync();
        }
    }

    public async Task RollbackAsync()
    {
        if (Session.IsInTransaction)
        {
            await Session.AbortTransactionAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Session != null)
        {
            await CastAndDispose(Session);
        }

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync();
            }
            else
            {
                resource.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Session?.Dispose();
    }
}