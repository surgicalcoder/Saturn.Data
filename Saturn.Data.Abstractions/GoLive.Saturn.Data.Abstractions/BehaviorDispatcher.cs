using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public static async ValueTask DispatchAfterAsync<TItem>(
        IList<IRepositoryWriteBehavior> behaviors,
        RepositoryWriteOperation operation,
        RepositoryWriteContext<TItem> context,
        RepositoryWriteResult result)
        where TItem : Entity
    {
        if (behaviors == null || behaviors.Count == 0)
        {
            return;
        }

        if (context.Suppress)
        {
            return;
        }

        foreach (var behavior in behaviors)
        {
            try
            {
                switch (operation)
                {
                    case RepositoryWriteOperation.Insert:
                        await behavior.AfterInsert(context, result);
                        break;
                    case RepositoryWriteOperation.Update:
                        await behavior.AfterUpdate(context, result);
                        break;
                    case RepositoryWriteOperation.Upsert:
                        await behavior.AfterUpsert(context, result);
                        break;
                    case RepositoryWriteOperation.Save:
                        await behavior.AfterSave(context, result);
                        break;
                    case RepositoryWriteOperation.Delete:
                        await behavior.AfterDelete(context, result);
                        break;
                    case RepositoryWriteOperation.HardDelete:
                        await behavior.AfterHardDelete(context, result);
                        break;
                    case RepositoryWriteOperation.Restore:
                        await behavior.AfterRestore(context, result);
                        break;
                    case RepositoryWriteOperation.Patch:
                        await behavior.AfterPatch(context, result);
                        break;
                    case RepositoryWriteOperation.Increment:
                        await behavior.AfterIncrement(context, result);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"After* hook '{behavior.GetType().Name}' for {operation} threw: {ex}");
            }
        }
    }

    public static async ValueTask DispatchOnWriteFailedAsync<TItem>(
        IList<IRepositoryWriteBehavior> behaviors,
        RepositoryWriteContext<TItem> context,
        Exception exception)
        where TItem : Entity
    {
        if (behaviors == null || behaviors.Count == 0 || context.Suppress)
        {
            return;
        }

        foreach (var behavior in behaviors)
        {
            try
            {
                await behavior.OnWriteFailed(context, exception);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnWriteFailed hook '{behavior.GetType().Name}' threw: {ex}");
            }
        }
    }
}
