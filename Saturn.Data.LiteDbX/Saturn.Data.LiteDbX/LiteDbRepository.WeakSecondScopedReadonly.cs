using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IWeakSecondScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await ById<TItem>(primaryScope, secondScope, id, false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var combined = ApplySoftDeleteFilter(scopePredicate.And(item => item.Id == id), includeDeleted);
        return await GetCollection<TItem>().FindOne(combined, cancellationToken: cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return ById<TItem>(primaryScope, secondScope, IDs, false, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);

        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TItem>());
        }

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        scopePredicate = ApplySoftDeleteFilter(scopePredicate, includeDeleted);

        var result = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(scopePredicate))
            .Where(Query.In("_id", normalizedIds))
            .ToEnumerable(cancellationToken);

        return Task.FromResult(result);
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return All<TItem>(primaryScope, secondScope, false, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        scopePredicate = ApplySoftDeleteFilter(scopePredicate, includeDeleted);
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(scopePredicate);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable(cancellationToken: cancellationToken));
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new()
    {
        return IQueryable<TItem>(primaryScope, secondScope, false);
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted = false)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        scopePredicate = ApplySoftDeleteFilter(scopePredicate, includeDeleted);
        return GetCollection<TItem>().AsQueryable().Where(scopePredicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return await Many(scopePredicate.And(predicate), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope;
        combinedWhereClause["SecondScope"] = secondScope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await One<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return await One(scopePredicate.And(predicate), continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await Count<TItem>(primaryScope, secondScope, predicate, continueFrom, false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return await Count(scopePredicate.And(predicate), continueFrom, includeDeleted, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await Random<TItem>(primaryScope, secondScope, predicate, continueFrom, count, false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var combinedPredicate = predicate == null ? scopePredicate : scopePredicate.And(predicate);
        return await Random(combinedPredicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var predicate = (Expression<Func<TItem, bool>>)(item => true);
        return Many(primaryScope, secondScope, predicate, selector, null, null, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<TProjection> ById<TItem, TProjection>(string primaryScope, string secondScope, string id,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var predicate = (Expression<Func<TItem, bool>>)(item => item.Id == id);
        return One(primaryScope, secondScope, predicate, selector, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(string primaryScope, string secondScope, IEnumerable<string> IDs,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);
        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TProjection>());
        }

        var predicate = (Expression<Func<TItem, bool>>)(item => normalizedIds.Contains(item.Id));
        return Many(primaryScope, secondScope, predicate, selector, null, normalizedIds.Count, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return Many(scopePredicate.And(predicate), selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope;
        combinedWhereClause["SecondScope"] = secondScope;
        return Many<TItem, TProjection>(combinedWhereClause, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<TProjection> One<TItem, TProjection>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return One(scopePredicate.And(predicate), selector, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
}
