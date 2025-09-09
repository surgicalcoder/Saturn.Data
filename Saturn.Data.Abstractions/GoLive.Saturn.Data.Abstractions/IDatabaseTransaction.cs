using System;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions;

public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
    Task Start();
}