using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : ITransparentScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken);
    }
    
    public async Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await Count<TItem, TParent>(scope, predicate, transaction, cancellationToken);
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return All<TItem, TParent>(scope, cancellationToken: cancellationToken);
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await Many<TItem, TParent>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await Many<TItem, TParent>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await One<TItem, TParent>(scope, predicate, continueFrom, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await Random<TItem, TParent>(scope, predicate, count, transaction, cancellationToken);
    }
}