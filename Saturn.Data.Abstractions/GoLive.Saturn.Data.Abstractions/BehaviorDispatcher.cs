using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static class BehaviorDispatcher
{
    public static async ValueTask DispatchBeforeAsync<TItem>(
        IList<IRepositoryWriteBehavior> behaviors,
        RepositoryWriteOperation operation,
        RepositoryWriteContext<TItem> context)
        where TItem : Entity
    {
        if (behaviors == null || behaviors.Count == 0)
        {
            return;
        }

        foreach (var behavior in behaviors)
        {
            switch (operation)
            {
                case RepositoryWriteOperation.Insert:
                    await behavior.BeforeInsert(context);
                    break;
                case RepositoryWriteOperation.Update:
                    await behavior.BeforeUpdate(context);
                    break;
                case RepositoryWriteOperation.Upsert:
                    await behavior.BeforeUpsert(context);
                    break;
                case RepositoryWriteOperation.Save:
                    await behavior.BeforeSave(context);
                    break;
                case RepositoryWriteOperation.Delete:
                    await behavior.BeforeDelete(context);
                    break;
                case RepositoryWriteOperation.HardDelete:
                    await behavior.BeforeHardDelete(context);
                    break;
                case RepositoryWriteOperation.Restore:
                    await behavior.BeforeRestore(context);
                    break;
                case RepositoryWriteOperation.Patch:
                    await behavior.BeforePatch(context);
                    break;
                case RepositoryWriteOperation.Increment:
                    await behavior.BeforeIncrement(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }
    }
}
