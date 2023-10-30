using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class Repository : IScopedReadonlyRepository
{
    public async Task<T> ById<T, T2>(T2 scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();

        return result;
    }

    public async Task<List<T>> ById<T, T2>(T2 scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return await result.ToListAsync().ConfigureAwait(false);
    }

    public async Task<IQueryable<T>> All<T, T2>(T2 scope) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope);

        return await Task.Run(() => scopedEntities);
    }

    public async Task<T> One<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        
        var findOptions = new FindOptions<T> { Limit = 1 };
        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<T> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }
        
        var result = await GetCollection<T>().FindAsync(combinedPred, findOptions);

        return await result.FirstOrDefaultAsync();
    }

    public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                scopedEntities = sortOrder.Direction == SortDirection.Ascending ? scopedEntities.OrderBy(e => sortOrder.Field.Invoke(e)) : scopedEntities.OrderByDescending(e => sortOrder.Field.Invoke(e));
            }
        }

        return await Task.Run(() => scopedEntities);
    }

    public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (pageSize == 0 || PageNumber == 0)
        {
            return await Many(scope, predicate).ConfigureAwait(false);
        }

        var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                res = sortOrder.Direction == SortDirection.Ascending ? res.OrderBy(e => sortOrder.Field.Invoke(e)) : res.OrderByDescending(e => sortOrder.Field.Invoke(e));
            }
        }

        res = res.Skip((PageNumber - 1) * pageSize).Take(pageSize);

        return await Task.Run(() => res);
    }

    public async Task<long> CountMany<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<T>().CountDocumentsAsync(combinedPred);
    }

    public async Task<T> ById<T, T2>(string scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await (await GetCollection<T>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();

        return result;
    }

    public async Task<List<T>> ById<T, T2>(string scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return await result.ToListAsync().ConfigureAwait(false);
    }

    public async Task<IQueryable<T>> All<T, T2>(string scope) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope);

        return await Task.Run(() => scopedEntities);
    }

    public async Task<T> One<T, T2>(string scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        
        var findOptions = new FindOptions<T> { Limit = 1 };
        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<T> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }
        
        var result = await GetCollection<T>().FindAsync(combinedPred, findOptions);

        return await result.FirstOrDefaultAsync();
    }

    public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                scopedEntities = sortOrder.Direction == SortDirection.Ascending ? scopedEntities.OrderBy(e => sortOrder.Field.Invoke(e)) : scopedEntities.OrderByDescending(e => sortOrder.Field.Invoke(e));
            }
        }

        return await Task.Run(() => scopedEntities);
    }

    public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<T, T2>(scope, predicate).ConfigureAwait(false);
        }

        var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(e => sortOrder.Field.Invoke(e)) : current.OrderByDescending(e => sortOrder.Field.Invoke(e)));
        }

        res = res.Skip((pageNumber - 1) * pageSize).Take(pageSize);

        return await Task.Run(() => res);
    }

    public async Task<long> CountMany<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<T>().CountDocumentsAsync(combinedPred);
    }
}