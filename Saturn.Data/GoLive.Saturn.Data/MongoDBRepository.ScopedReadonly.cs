using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.AsyncEnumerable;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TScope>(TScope scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 })).FirstOrDefaultAsync();

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(TScope scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }
    
    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return scopedEntities;
    }

    public async Task<TItem> ById<TItem, TScope>(string scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 })).FirstOrDefaultAsync();

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        
        var findOptions = new FindOptions<TItem> { Limit = 1 };
        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<TItem> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }
        
        var result = await GetCollection<TItem>().FindAsync(combinedPred, findOptions);

        return await result.FirstOrDefaultAsync();
    }

    public async Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                scopedEntities = sortOrder.Direction == SortDirection.Ascending ? scopedEntities.OrderBy(sortOrder.Field) : scopedEntities.OrderByDescending(sortOrder.Field);
            }
        }

        return await Task.Run(() => scopedEntities);
    }

    public async Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem, TScope>(scope, predicate).ConfigureAwait(false);
        }

        var res = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        res = res.Skip((pageNumber - 1) * pageSize).Take(pageSize);

        return await Task.Run(() => res);
    }

    public async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<TItem>().CountDocumentsAsync(combinedPred);
    }
}