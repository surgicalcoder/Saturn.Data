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
    public static async Task Populate<TItem, TParent>(this Ref<TItem> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (item == null || repository == null)
        {
            return;
        }
        item.Item = await repository.ById<TItem, TParent>(item.Id, transaction: transaction, cancellationToken: cancellationToken);
    }
        

    public static async Task Populate<TItem, TParent>(this List<Ref<TItem>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = await (await repository.ById<TItem, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }
    
    public static async Task Populate<TItem, TParent>(this IList<Ref<TItem>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = await (await repository.ById<TItem, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }

    public static async Task<List<TItem>> Populate<TItem, T2, TParent>(this List<TItem> collection, Expression<Func<TItem, Ref<T2>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return null;
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
            
        var items = await (await repository.ById<T2, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        collection.ForEach(delegate (TItem obj)
        {
            var re = compile(obj);

            if (re != null)
            {
                re.Fetch(items);
            }
        });

        return collection;
    }
    
    public static async Task<IList<TItem>> Populate<TItem, T2, TParent>(this IList<TItem> collection, Expression<Func<TItem, Ref<T2>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
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
            
        var items = await (await repository.ById<T2, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

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

    public static async Task<List<TItem>> PopulateMultiple<TItem, T2, TParent>(this List<TItem> collection, Expression<Func<TItem, List<Ref<T2>>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

        var IDs = collection.SelectMany(f => compiledFunc(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        
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
    
    public static async Task<IList<T>> PopulateMultiple<T, T2, TParent>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, ITransparentScopedRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where T2 : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

        var IDs = collection.SelectMany(f => compiledFunc(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2, TParent>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        
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