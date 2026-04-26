using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IWeakScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;

        return await GetCollection<TItem>().FindOne(scopePredicate.And(idPredicate), cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);

        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TItem>());
        }

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);

        var result = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(scopePredicate))
            .Where(Query.In("_id", normalizedIds))
            .ToEnumerable(cancellationToken);

        return Task.FromResult(result);
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(scopePredicate);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return GetCollection<TItem>().AsQueryable().Where(scopePredicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return await Many(scopePredicate.And(predicate), continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = scope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return await One(scopePredicate.And(predicate), continueFrom, sortOrders, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return await Count(scopePredicate.And(predicate), continueFrom, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = predicate == null ? scopePredicate : scopePredicate.And(predicate);
        return await Random(combinedPredicate, continueFrom, count, transaction, cancellationToken);
    }
}
