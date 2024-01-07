using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static partial class PopulateHelper
{
    public static async Task Populate<T>(this Ref<T> item, IReadonlyRepository repository) where T : Entity, new()
    {
        if (item == null)
        {
            return;
        }
        item.Item = await repository.ByRef(item);
    }

    public static void Populate<T>(this Ref<T> item, IList<T> items) where T : Entity, new()
    {            
        if (item == null)
        {
            return;
        }
            
        item.Item = items.FirstOrDefault(f => f.Id == item.Id);
    }

    public static async Task Populate<T>(this IList<Ref<T>> item, IReadonlyRepository repository) where T : Entity, new()
    {
        if (item == null || item.Count == 0)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = (await repository.Many<T>(f => IDs.Contains(f.Id))).ToList();
            
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }
        
    public static async Task Populate<T>(this IList<Ref<T>> item, IList<T> items) where T : Entity, new()
    {
        if (item == null || item.Count == 0)
        {
            return;
        }
            
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }

    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IReadonlyRepository repository) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0)
        {
            return default;
        }
            
        var compiledFunc = item.Compile();

        var IDs = collection.Where(f=>
        {
            try
            {
                return compiledFunc.Invoke(f) != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }).Select(f => compiledFunc.Invoke(f)).Select(r => r.Id).ToList();
            
        if (IDs.Count == 0)
        {
            return collection;
        }
            
        var items = (await repository.Many<T2>(f => IDs.Contains(f.Id))).ToList();

        for (var i = 0; i < collection.Count; i++)
        {
            var result = compiledFunc.Invoke(collection[i]);

            if (result != null)
            {
                result.Fetch(items);
            }
        }

        return collection;
    }

#pragma warning disable 1998
    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IList<T2> items) where T2 : Entity, new()
#pragma warning restore 1998
    {
        if (collection == null || collection.Count == 0)
        {
            return default;
        }
            
        var compiledFunc = item.Compile();

        for (var i = 0; i < collection.Count; i++)
        {
            var result = compiledFunc.Invoke(collection[i]);

            if (result != null)
            {
                result.Fetch(items);
            }
        }

        return collection;
    }

    public static async Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IReadonlyRepository repository) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0)
        {
            return default;
        }
            
        var compile = item.Compile();

        var IDs = collection.SelectMany(f => compile.Invoke(f)).Select(r => r.Id).ToList();

        var items = (await repository.Many<T2>(f => IDs.Contains(f.Id))).ToList();

        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compile.Invoke(collection[i]))
            {
                r2.Fetch(items);
            }
        }

        return collection;
    }

#pragma warning disable 1998
    public static async Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IList<T2> items) where T2 : Entity, new()
#pragma warning restore 1998
    {
        if (collection == null || collection.Count == 0)
        {
            return default;
        }
            
        var compile = item.Compile();
            
        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compile.Invoke(collection[i]))
            {
                r2.Fetch(items);
            }
        }

        return collection;
    }
}