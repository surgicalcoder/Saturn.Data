using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : ISecondScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var result = (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id)).FirstOrDefault();

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id);

        return result.ToAsyncEnumerable();
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id);
        return await Many<TItem>(combinedPredicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope.Id;
        combinedWhereClause["SecondScope"] = secondScope.Id;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id);
        var results = await Many<TItem>(combinedPredicate, continueFrom, 1, null, sortOrders, transaction, cancellationToken);
        return await results.FirstOrDefaultAsync(cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate == null ? (Expression<Func<TItem, bool>>)(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id) : predicate.And<TItem>(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id);
        var results = await Many<TItem>(combinedPredicate, null, count, null, null, transaction, cancellationToken);
        return results;
    }
    public virtual async Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = firstPred.And(TransformRefEntityComparisons(predicate));

        return await GetCollection<TItem>().LongCountAsync(combinedPred);
    }
}