using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IRepositoryWriteBehavior
{
    ValueTask BeforeInsert<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeUpdate<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeUpsert<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeSave<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeDelete<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeHardDelete<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeRestore<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforePatch<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;

    ValueTask BeforeIncrement<TItem>(RepositoryWriteContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;
}

