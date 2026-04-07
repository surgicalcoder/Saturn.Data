using GoLive.Saturn.Data.Abstractions;
using LiteDbX.Engine;

namespace Saturn.Data.LiteDbX;

public class LiteDbXTransactionWrapper(ILiteTransaction item) : IDatabaseTransaction
{
    public async ValueTask DisposeAsync()
    {
        await item.DisposeAsync();
    }

    public async Task CommitAsync()
    {
        await item.Commit();
    }
    public async Task RollbackAsync()
    {
        await item.Rollback();
    }
    public Task Start()
    {
        return Task.CompletedTask;
    }
}