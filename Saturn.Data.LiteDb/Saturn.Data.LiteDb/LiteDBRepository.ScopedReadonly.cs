using System.Linq.Expressions;
using System.Net.NetworkInformation;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IScopedReadonlyRepository
{
    public async Task<T> ById<T, T2>(T2 scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        return await GetCollection<T>().FindOneAsync(e => e.Id == id && e.Scope == scope.Id);
    }

    public async Task<IAsyncEnumerable<T>> ById<T, T2>(T2 scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope.Id);

        return result.ToAsyncEnumerable();
    }

    public IQueryable<T> All<T, T2>(T2 scope) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope.Id);

        return scopedEntities;
    }

    public async Task<T> One<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope.Id;
        var combinedPred = firstPred.And(predicate);

        var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                res = sortOrder.Direction == SortDirection.Ascending ? res.OrderBy(sortOrder.Field) : res.OrderByDescending(sortOrder.Field);
            }
        }

        return res.FirstOrDefault();
    }

    public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope.Id).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                scopedEntities = sortOrder.Direction == SortDirection.Ascending ? scopedEntities.OrderBy(sortOrder.Field) : scopedEntities.OrderByDescending(sortOrder.Field);
            }
        }

        return await Task.Run(() => scopedEntities);
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public async Task<IQueryable<T>> Many<T, T2>(T2 scope, Expression<Func<T, bool>> predicate, int pageSize, int PageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return null;
        }
        
        if (pageSize == 0 || PageNumber == 0)
        {
            return await Many(scope, predicate).ConfigureAwait(false);
        }

        var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope.Id).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                res = sortOrder.Direction == SortDirection.Ascending ? res.OrderBy(sortOrder.Field) : res.OrderByDescending(sortOrder.Field);
            }
        }

        res = res.Skip((PageNumber - 1) * pageSize).Take(pageSize);

        return await Task.Run(() => res);
    }

    public async Task<long> CountMany<T, T2>(T2 scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.Id))
        {
            return 0;
        }
        
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope.Id;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<T>().LongCountAsync(combinedPred);
    }

    public async Task<T> ById<T, T2>(string scope, string id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        return await GetCollection<T>().FindOneAsync(e => e.Id == id && e.Scope == scope);
    }

    public async Task<IAsyncEnumerable<T>> ById<T, T2>(string scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<T>> All<T, T2>(string scope) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope);

        return await Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public async Task<T> One<T, T2>(string scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        var res = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                res = sortOrder.Direction == SortDirection.Ascending ? res.OrderBy(sortOrder.Field) : res.OrderByDescending(sortOrder.Field);
            }
        }

        return res.FirstOrDefault();
    }

    public async Task<IQueryable<T>> Many<T, T2>(string scope, Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var scopedEntities = GetCollection<T>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                scopedEntities = sortOrder.Direction == SortDirection.Ascending ? scopedEntities.OrderBy(sortOrder.Field) : scopedEntities.OrderByDescending(sortOrder.Field);
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
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        res = res.Skip((pageNumber - 1) * pageSize).Take(pageSize);

        return await Task.Run(() => res);
    }

    public async Task<long> CountMany<T, T2>(string scope, Expression<Func<T, bool>> predicate) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        Expression<Func<T, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<T>().LongCountAsync(combinedPred);
    }
}