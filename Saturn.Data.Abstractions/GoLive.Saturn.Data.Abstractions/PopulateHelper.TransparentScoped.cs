using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FastExpressionCompiler;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static partial class PopulateHelper
{
    public static async Task Populate<T>(this Ref<T> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || repository == null)
        {
            return;
        }
        item.Item = await repository.ByRef(item, cancellationToken);
    }
        

    public static async Task Populate<T>(this List<Ref<T>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = await (await repository.ById<T>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }
    
    public static async Task Populate<T>(this IList<Ref<T>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = await (await repository.ById<T>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }

    public static async Task<List<T>> Populate<T, T2>(this List<T> collection, Expression<Func<T, Ref<T2>>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compile = item.CompileFast();

        var IDs = collection.Where(f=>
        {
            try
            {
                return compile(f) != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }).Select(f => compile(f)).Select(r => r.Id).ToList();
            
        if (IDs.Count == 0)
        {
            return collection;
        }
            
        var items = await (await repository.ById<T2>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        collection.ForEach(delegate (T obj)
        {
            var re = compile(obj);

            if (re != null)
            {
                re.Fetch(items);
            }
        });

        return collection;
    }
    
    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

        var IDs = collection.Where(f=>
        {
            try
            {
                return compiledFunc(f) != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }).Select(f => compiledFunc(f)).Select(r => r.Id).ToList();
            
        if (IDs.Count == 0)
        {
            return collection;
        }
            
        var items = await (await repository.ById<T2>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        for (var i = 0; i < collection.Count; i++)
        {
            var result = compiledFunc(collection[i]);

            if (result != null)
            {
                result.Fetch(items);
            }
        }

        return collection;
    }

    public static async Task<List<T>> PopulateMultiple<T, T2>(this List<T> collection, Expression<Func<T, List<Ref<T2>>>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

        var IDs = collection.SelectMany(f => compiledFunc(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        
        for (var i = 0; i < collection.Count; i++)
        {
            var result = compiledFunc(collection[i]);

            if (result == null)
            {
                continue;
            }

            for (var i1 = 0; i1 < result.Count; i1++)
            {
                result[i1].Fetch(items);
            }
        }

        return collection;
    }
    
    public static async Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, ITransparentScopedRepository repository, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

        var IDs = collection.SelectMany(f => compiledFunc(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2>(IDs, cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        
        for (var i = 0; i < collection.Count; i++)
        {
            var result = compiledFunc(collection[i]);

            if (result == null)
            {
                continue;
            }

            for (var i1 = 0; i1 < result.Count; i1++)
            {
                result[i1].Fetch(items);
            }
        }

        return collection;
    }

}