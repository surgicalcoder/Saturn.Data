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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));

        return await GetCollection<TItem>().FindOne(scopePredicate.And(e => e.Id == id), cancellationToken: cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var normalizedIds = NormalizeEntityIds(IDs);

        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TItem>());
        }

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));

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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(scopePredicate);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return GetCollection<TItem>().AsQueryable().Where(scopePredicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return await Many(scopePredicate.And(predicate), continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = primaryScope;
        combinedWhereClause["SecondScope"] = secondScope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var results = await Many(scopePredicate.And(predicate), continueFrom, 1, null, sortOrders, transaction, cancellationToken);
        return await results.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return await Count(scopePredicate.And(predicate), continueFrom, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var combinedPredicate = predicate == null ? scopePredicate : scopePredicate.And(predicate);
        var results = await Many(combinedPredicate, continueFrom, count, null, null, transaction, cancellationToken);
        return results;
    }
}
