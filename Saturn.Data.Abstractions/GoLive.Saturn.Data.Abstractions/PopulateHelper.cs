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
    public static async Task Populate<T>(this Ref<T> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id) || repository == null)
        {
            return;
        }
        item.Item = await repository.ByRef(item, transaction: transaction, cancellationToken: cancellationToken);
    }
    
    public static void Populate<T>(this Ref<T> item, IList<T> items) where T : Entity, new()
    {            
        if (item == null || items == null || string.IsNullOrWhiteSpace(item.Id))
        {
            return;
        }
            
        item.Item = items.FirstOrDefault(f => f.Id == item.Id);
    }

    public static async Task Populate<T>(this List<Ref<T>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id);
        var items = await (await repository.ById<T>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
            
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }
    
    public static async Task Populate<T>(this IList<Ref<T>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || repository == null)
        {
            return;
        }
            
        var IDs = item.Select(f => f.Id).ToList();
        var items = await (await repository.ById<T>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
            
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }
    
    public static void Populate<T>(this IList<Ref<T>> item, IList<T> items) where T : Entity, new()
    {
        if (item == null || item.Count == 0 || items == null || items.Count == 0)
        {
            return;
        }
            
        for (var i = 0; i < item.Count; i++)
        {
            item[i].Fetch(items);
        }
    }

    public static async Task<List<T>> Populate<T, T2>(this List<T> collection, Expression<Func<T, Ref<T2>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return null;
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

        var items = await (await repository.ById<T2>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

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
    
    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return null;
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
            
        var items = await (await repository.ById<T2>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

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
    
    public static async Task<List<T>> Populate<T, T2>(this List<T> collection, Expression<Func<T, Ref<T2>>> item, List<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || items == null || items.Count == 0)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

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
    
    public static async Task<IList<T>> Populate<T, T2>(this IList<T> collection, Expression<Func<T, Ref<T2>>> item, IList<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || items == null || items.Count == 0)
        {
            return default;
        }
            
        var compiledFunc = item.CompileFast();

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

    public static async Task<List<T>> PopulateMultiple<T, T2>(this List<T> collection, Expression<Func<T, List<Ref<T2>>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return default;
        }
            
        var compile = item.CompileFast();

        var IDs = collection.SelectMany(f => compile(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        for (var i = 0; i < collection.Count; i++)
        {
            var r2s = compile(collection[i]);

            for (var i2 = 0; i2 < r2s.Count; i2++)
            {
                var r2 = r2s[i2];
                r2.Fetch(items);
            }
        }

        return collection;
    }
    
    public static async Task<IList<T>> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IReadonlyRepository repository, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || repository == null)
        {
            return null;
        }
            
        var compile = item.CompileFast();

        var IDs = collection.SelectMany(f => compile(f)).Select(r => r.Id).ToList();

        var items = await (await repository.ById<T2>(IDs, transaction: transaction, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);

        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compile(collection[i]))
            {
                r2.Fetch(items);
            }
        }

        return collection;
    }

    public static List<T> PopulateMultiple<T, T2>(this List<T> collection, Expression<Func<T, List<Ref<T2>>>> item, List<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || items == null || items.Count == 0)
        {
            return null;
        }
            
        var compile = item.CompileFast();
            
        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compile(collection[i]))
            {
                r2.Fetch(items);
            }
        }

        return collection;
    }
    
    public static IList<T> PopulateMultiple<T, T2>(this IList<T> collection, Expression<Func<T, IList<Ref<T2>>>> item, IList<T2> items) where T2 : Entity, new()
    {
        if (collection == null || collection.Count == 0 || item == null || items == null || items.Count == 0)
        {
            return null;
        }
            
        var compile = item.CompileFast();
            
        for (var i = 0; i < collection.Count; i++)
        {
            foreach (var r2 in compile(collection[i]))
            {
                r2.Fetch(items);
            }
        }

        return collection;
    }
}