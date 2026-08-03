using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public sealed class RecordingWriteBehavior : IRepositoryWriteBehavior
{
    private readonly ConcurrentQueue<RecordedCall> calls = new();

    public IReadOnlyList<RecordedCall> Calls => calls.ToList();

    public IReadOnlyList<RepositoryWriteResult> AfterResults(RepositoryWriteOperation operation)
        => calls.Where(c => c.Operation == operation).Select(c => c.Result).ToList();

    public void Clear() => calls.Clear();

    public sealed record RecordedCall(RepositoryWriteOperation Operation, RepositoryWriteResult Result);

    private void Record(RepositoryWriteOperation operation, RepositoryWriteResult result) => calls.Enqueue(new RecordedCall(operation, result));

    public ValueTask AfterInsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Insert, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterUpdate<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Update, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterUpsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Upsert, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterSave<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Save, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Delete, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterHardDelete<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.HardDelete, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterRestore<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Restore, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterPatch<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Patch, result);
        return ValueTask.CompletedTask;
    }

    public ValueTask AfterIncrement<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
    {
        Record(RepositoryWriteOperation.Increment, result);
        return ValueTask.CompletedTask;
    }
}
