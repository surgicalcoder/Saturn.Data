using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
// var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
namespace Saturn.Data.Stellar;

public partial class StellarRepository : ITransparentScopedReadonlyRepository 
{
    protected virtual string GetTransparentScope<TParent>() where TParent : Entity, new()
    {
        return options.TransparentScopeProvider.Invoke(typeof(TParent));
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await All<TItem, TParent>(scope, transaction, cancellationToken);
    }
    
    public async Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await ById<TItem, TParent>(scope, id, transaction, cancellationToken);
    }
    
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken);
    }
    
    public async Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await Count<TItem, TParent>(scope, predicate, continueFrom, transaction, cancellationToken);
    }
    
    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return IQueryable<TItem, TParent>(scope);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await Many<TItem, TParent>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await Many<TItem, TParent>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await One<TItem, TParent>(scope, predicate, continueFrom, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = GetTransparentScope<TParent>();
        return await Random<TItem, TParent>(scope, predicate, continueFrom, count, transaction, cancellationToken);
    }
}
