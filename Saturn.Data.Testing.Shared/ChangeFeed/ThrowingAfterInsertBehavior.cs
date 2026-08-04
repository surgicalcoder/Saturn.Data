using System;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public class ThrowingAfterInsertBehavior : IRepositoryWriteBehavior
{
    public ValueTask AfterInsert<TItem>(RepositoryWriteContext<TItem> context, RepositoryWriteResult result) where TItem : Entity
        => throw new InvalidOperationException("Intentional failure for error isolation test");
}