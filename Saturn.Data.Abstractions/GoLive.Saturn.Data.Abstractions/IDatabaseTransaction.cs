using System;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions;

public interface IDatabaseTransaction : IAsyncDisposable
{
    Task Start();
    Task CommitAsync();
    Task RollbackAsync();
}