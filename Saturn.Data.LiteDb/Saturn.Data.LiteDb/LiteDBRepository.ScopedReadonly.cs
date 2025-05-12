using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        return await GetCollection<TItem>().FindOneAsync(e => e.Id == id && e.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        var res = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(combinedPred);

        if (sortOrders != null)
        {
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return res.FirstOrDefaultAsync(cancellationToken);
    }

    public IQueryable<TItem> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            scopedEntities = sortOrders.Aggregate(scopedEntities, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return scopedEntities;
    }

    public async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return 0;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<TItem>().LongCountAsync(combinedPred);
    }
}