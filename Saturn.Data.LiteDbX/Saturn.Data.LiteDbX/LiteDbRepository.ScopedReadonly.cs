using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using Saturn.Data.LiteDbX; // For ObjectId
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;


namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await ById<TItem, TScope>(scope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }
        // Rewrite scope comparison to ObjectId
        Expression<Func<TItem, bool>> pred = e => e.Id == id && e.Scope == scope;
        pred = ApplySoftDeleteFilter(pred, includeDeleted);

        return await GetCollection<TItem>().FindOne(pred, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await ById<TItem, TScope>(scope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var normalizedIds = NormalizeEntityIds(IDs);

        if (normalizedIds.Count == 0)
        {
            return EmptyAsyncEnumerable<TItem>();
        }

        Expression<Func<TItem, bool>> scopePredicate = entity => entity.Scope == scope;
        scopePredicate = ApplySoftDeleteFilter(scopePredicate, includeDeleted);

        var result = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(scopePredicate))
            .Where(Query.In("_id", normalizedIds))
            .ToEnumerable(cancellationToken);

        return result;
    }


    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return All<TItem, TScope>(scope, includeDeleted: false, transaction, cancellationToken);
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }
        Expression<Func<TItem, bool>> pred = f => f.Scope == scope;
        pred = ApplySoftDeleteFilter(pred, includeDeleted);

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(pred);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable(cancellationToken: cancellationToken));
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return IQueryable<TItem, TScope>(scope, includeDeleted: false);
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TScope>(string scope, bool includeDeleted = false) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> pred = f => f.Scope == scope;
        pred = ApplySoftDeleteFilter(pred, includeDeleted);

        return GetCollection<TItem>().AsQueryable().Where(pred);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Many<TItem, TScope>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);

        return await Many<TItem>(combinedPredicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Many<TItem, TScope>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = scope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await One<TItem, TScope>(scope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);

        return await One<TItem>(combinedPredicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    
    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Count<TItem, TScope>(scope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return 0;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        return await Count<TItem>(combinedPred, continueFrom, includeDeleted, transaction, cancellationToken);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Random<TItem, TScope>(scope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null,
        int count = 1, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate == null ? (item => item.Scope == scope) : predicate.And<TItem>(e => e.Scope == scope);

        return await Random<TItem>(combinedPredicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var scopePredicate = (Expression<Func<TItem, bool>>)(item => item.Scope == scope);
        return Many<TItem, TScope, TProjection>(scope, scopePredicate, selector, null, null, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<TProjection> ById<TItem, TScope, TProjection>(string scope, string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var predicate = (Expression<Func<TItem, bool>>)(item => item.Id == id && item.Scope == scope);
        return One<TItem, TScope, TProjection>(scope, predicate, selector, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TScope, TProjection>(string scope, IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var normalizedIds = NormalizeEntityIds(ids);

        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TProjection>());
        }

        var predicate = (Expression<Func<TItem, bool>>)(item => normalizedIds.Contains(item.Id));
        return Many<TItem, TScope, TProjection>(scope, predicate, selector, null, normalizedIds.Count, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == scope);
        return Many(combinedPredicate, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Dictionary<string, object> whereClause,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = scope;
        return Many<TItem, TProjection>(combinedWhereClause, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public Task<TProjection> One<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(item => item.Scope == scope);
        return One(combinedPredicate, selector, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
}