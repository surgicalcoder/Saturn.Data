using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : ISecondScopedRepository
{
    public async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var result = (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id)).FirstOrDefault();

        return result;
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id);
    }

    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = firstPred.And(predicate);

        var query = GetCollection<TItem>().Query().Where(combinedPred);

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return await query.FirstOrDefaultAsync();
    }

    public IQueryable<TItem> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate,
        int pageSize = 20, int pageNumber = 1, IEnumerable<SortOrder<TItem>> sortOrders = null)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var res = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id).Where(predicate);

        if (sortOrders != null)
        {
            res = sortOrders.Aggregate(res, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        if (pageSize != 0 && pageNumber != 0)
        {
            res = res.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        return res;
    }

    public async Task<long> CountMany<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = firstPred.And(predicate);

        return await GetCollection<TItem>().LongCountAsync(combinedPred);
    }

    public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope;
        entity.SecondScope = secondScope;
        await Insert(entity, cancellationToken: cancellationToken);
    }
    
    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new() {        entity.Scope = primaryScope;
        entity.SecondScope = secondScope;
        await Update(entity, cancellationToken: cancellationToken); }
    public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)  where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new() 
        where TPrimaryScope : Entity, new() {         entity.Scope = primaryScope;
        entity.SecondScope = secondScope;
        await Upsert(entity, cancellationToken: cancellationToken);}

    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string Id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        await Delete<TItem>(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id && e.Id == Id, cancellationToken: cancellationToken);
    }
}