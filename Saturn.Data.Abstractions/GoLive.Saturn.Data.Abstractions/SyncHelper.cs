using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions;

public static class SyncHelper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadablePropsCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> WritablePropsCache = new();

    internal static void CopyPropertiesTo<T, TU>(this T source, TU dest)
    {
        var sourceProps = ReadablePropsCache.GetOrAdd(typeof(T), t =>
            t.GetProperties().Where(x => x.CanRead && x.Name != "Id").ToArray());
        var destProps = WritablePropsCache.GetOrAdd(typeof(TU), t =>
            t.GetProperties().Where(x => x.CanWrite && x.Name != "Id").ToArray());

        foreach (var sourceProp in sourceProps)
        {
            var destProp = destProps.FirstOrDefault(x => x.Name == sourceProp.Name);
            if (destProp == null) continue;
            if (!destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType)) continue;

            destProp.SetValue(dest, sourceProp.GetValue(source, null), null);
        }
    }

    public static async Task<List<TLocal>> SyncFrom<TLocal, TRemote>(
        this List<TLocal> local,
        List<TRemote> remote,
        Func<TLocal, TRemote, bool> identifier,
        Func<TLocal, TRemote, TLocal> performAssignments,
        Func<List<TLocal>, Task> itemsToDeleteFunc,
        Func<List<TLocal>, Task> itemsToUpdateFunc,
        Func<List<TLocal>, Task> itemsToAddFunc,
        CancellationToken ct = default
    ) where TLocal : new()
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        var actualList = new List<TLocal>();
        var itemsAdded = new List<TLocal>();

        foreach (TRemote remoteItem in remote)
        {
            ct.ThrowIfCancellationRequested();

            var item = local.FirstOrDefault(f => f != null && identifier(f, remoteItem));
            bool itemAdded = false;

            if (item == null)
            {
                item = new TLocal();
                itemAdded = true;
            }

            if (performAssignments != null)
            {
                item = performAssignments(item, remoteItem) ?? item;
            }
            else
            {
                remoteItem.CopyPropertiesTo(item);
            }

            if (itemAdded)
                itemsAdded.Add(item);
            else
                actualList.Add(item);
        }

        var toDelete = local.Except(actualList).Except(itemsAdded).ToList();

        var tasks = new List<Task>();
        if (itemsToDeleteFunc != null) tasks.Add(itemsToDeleteFunc(toDelete));
        if (itemsToUpdateFunc != null) tasks.Add(itemsToUpdateFunc(actualList));
        if (itemsToAddFunc != null) tasks.Add(itemsToAddFunc(itemsAdded));

        await Task.WhenAll(tasks);

        return actualList.Concat(itemsAdded).ToList();
    }
}