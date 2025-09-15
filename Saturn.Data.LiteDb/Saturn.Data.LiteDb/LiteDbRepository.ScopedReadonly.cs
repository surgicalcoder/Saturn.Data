using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        return await GetCollection<TItem>().FindOneAsync(e => e.Id == id && e.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope);

        return result.ToAsyncEnumerable();
    }


    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);
        return await Many<TItem>(combinedPredicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = scope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);
        return await One<TItem>(combinedPredicate, continueFrom, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate == null ? (item => item.Scope == scope) : predicate.And<TItem>(e => e.Scope == scope);
        return await Random<TItem>(combinedPredicate, count, transaction, cancellationToken);
    }

    public virtual async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
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