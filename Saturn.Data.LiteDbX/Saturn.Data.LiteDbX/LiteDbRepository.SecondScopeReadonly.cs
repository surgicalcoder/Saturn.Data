using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : ISecondScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        return await ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, id, false, transaction, cancellationToken);
    }

    public virtual async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        string id, bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Id == id && item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        predicate = ApplySoftDeleteFilter(predicate, includeDeleted);
        var result = GetCollection<TItem>().FindOne(predicate, cancellationToken: cancellationToken);
        return await result;
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, IDs, false, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IEnumerable<string> IDs, bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);
        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TItem>());
        }

        Expression<Func<TItem, bool>> scopePredicate = entity => entity.Scope == primaryScope.Id && entity.SecondScope == secondScope.Id;
        scopePredicate = ApplySoftDeleteFilter(scopePredicate, includeDeleted);

        var result = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(scopePredicate))
            .Where(Query.In("_id", normalizedIds))
            .ToEnumerable(cancellationToken);

        return Task.FromResult(result);
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        return All<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, false, transaction, cancellationToken);
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        predicate = ApplySoftDeleteFilter(predicate, includeDeleted);
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(predicate);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable(cancellationToken: cancellationToken));
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        return IQueryable<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, false);
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        bool includeDeleted = false)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        predicate = ApplySoftDeleteFilter(predicate, includeDeleted);
        return GetCollection<TItem>().AsQueryable().Where(predicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return await Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return await Many<TItem>(combinedPredicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return await Many<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope.Id;
        combinedWhereClause["SecondScope"] = secondScope.Id;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return await One<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return await One<TItem>(combinedPredicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    
    
    public async Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return await Count<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = firstPred.And(predicate);

        return await Count<TItem>(combinedPred, continueFrom, includeDeleted, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        return await Random<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, predicate, continueFrom, count, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate == null
            ? (Expression<Func<TItem, bool>>)(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id)
            : predicate.And<TItem>(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return await Random<TItem>(combinedPredicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var predicate = (Expression<Func<TItem, bool>>)(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return Many<TItem, TSecondScope, TPrimaryScope, TProjection>(primaryScope, secondScope, predicate, selector, null, null, null, null, includeDeleted,
            transaction, cancellationToken);
    }

    public Task<TProjection> ById<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var predicate = (Expression<Func<TItem, bool>>)(item => item.Id == id && item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return One<TItem, TSecondScope, TPrimaryScope, TProjection>(primaryScope, secondScope, predicate, selector, null, null, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, IEnumerable<string> IDs, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);
        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TProjection>());
        }

        var predicate = (Expression<Func<TItem, bool>>)(item => normalizedIds.Contains(item.Id));
        return Many<TItem, TSecondScope, TPrimaryScope, TProjection>(primaryScope, secondScope, predicate, selector, null, normalizedIds.Count, null, null,
            includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return Many(combinedPredicate, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope.Id;
        combinedWhereClause["SecondScope"] = secondScope.Id;
        return Many<TItem, TProjection>(combinedWhereClause, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<TProjection> One<TItem, TSecondScope, TPrimaryScope, TProjection>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == primaryScope.Id && item.SecondScope == secondScope.Id);
        return One(combinedPredicate, selector, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
}