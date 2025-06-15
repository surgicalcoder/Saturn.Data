using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        return await GetCollection<TItem>().FindOneAsync(e => e.Id == id && e.Scope == scope);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public virtual Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(TransformRefEntityComparisons(predicate));

        var res = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(combinedPred);

        if (sortOrders != null)
        {
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return res.FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(TransformRefEntityComparisons(predicate));

        if (sortOrders != null)
        {
            scopedEntities = sortOrders.Aggregate(scopedEntities, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        if (pageSize.HasValue && pageNumber.HasValue && pageSize.Value > 0 && pageNumber.Value > 0)
        {
            scopedEntities = scopedEntities.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value);
        }

        return scopedEntities.ToAsyncEnumerable();
    }

    public virtual async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return 0;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<TItem>().LongCountAsync(combinedPred);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(
        Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate,
        int pageSize = 20,
        int pageNumber = 1,
        IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new())
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var query = GetCollection<TItem>()
                    .AsQueryable()
                    .Where(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id)
                    .Where(TransformRefEntityComparisons(predicate));

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) =>
                sortOrder.Direction == SortDirection.Ascending
                    ? current.OrderBy(sortOrder.Field)
                    : current.OrderByDescending(sortOrder.Field));
        }

        if (pageSize > 0 && pageNumber > 0)
        {
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        return await Task.FromResult(query.ToAsyncEnumerable());
    }
}