﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions;

public static class SyncHelper
{
    public static async Task<List<TLocal>> SyncFrom<TLocal, TRemote>(
        this List<TLocal> Local,
        List<TRemote> Remote,
        Func<TLocal, TRemote, bool> Identifier,
        Func<TLocal, TRemote, TLocal> PerformAssignments,
        Func<List<TLocal>, Task> ItemsToDeleteFunc,
        Func<List<TLocal>, Task> ItemsToUpdateFunc,
        Func<List<TLocal>, Task> ItemsToAddFunc
    ) where TLocal : new()
    {
        var actualList = new List<TLocal>();
        var itemsAdded = new List<TLocal>();

        foreach (TRemote remoteItem in Remote)
        {
            var item = Local.FirstOrDefault(f => Identifier(f, remoteItem));
            bool itemAdded = false;
                
            if (item == null)
            {
                item = new TLocal();
                itemAdded = true;
            }

            if (PerformAssignments != null)
            {
                item = PerformAssignments(item, remoteItem);
            }
            else
            {
                remoteItem.CopyPropertiesTo(item);
            }

            if (itemAdded)
            {
                itemsAdded.Add(item);
            }
            else
            {
                actualList.Add(item);
            }
        }

        var toDelete = Local.Except(actualList).ToList();

        if (ItemsToDeleteFunc != null)
        {
            await ItemsToDeleteFunc(toDelete);
        }

        if (ItemsToUpdateFunc != null)
        {
            await ItemsToUpdateFunc(actualList);
        }

        if (ItemsToAddFunc != null)
        {
            await ItemsToAddFunc(itemsAdded);
        }
            
        return Local;
    }

    private static void CopyPropertiesTo<T, TU>(this T source, TU dest)
    {
        var sourceProps = typeof(T).GetProperties().Where(x => x.CanRead).Where(f => f.Name != "Id").ToList();
        var destProps = typeof(TU).GetProperties().Where(x => x.CanWrite).Where(f => f.Name != "Id" ).ToList();

        foreach (var sourceProp in sourceProps)
        {
            if (destProps.Any(x => x.Name == sourceProp.Name))
            {
                var p = destProps.First(x => x.Name == sourceProp.Name);
                p.SetValue(dest, sourceProp.GetValue(source, null), null);
            }

        }

    }
}