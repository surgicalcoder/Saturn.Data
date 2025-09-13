using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ITransparentScopedReadonlyRepository {
    async Task<TItem> ITransparentScopedReadonlyRepository.ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await ById<TItem, TParent>(scope, id, transaction, cancellationToken);
    }

    public async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken);
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> items, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var resultItems = await Many<TItem, TParent>(scope, e=> items.Select(r => r.Id).Contains(e.Id), cancellationToken: cancellationToken);
        
        var entityDict = await resultItems.ToDictionaryAsync(e => e.Id, cancellationToken);
        var result = new List<Ref<TItem>>();
        foreach (var item in items)
        {
            if (entityDict.TryGetValue(item.Id, out var entity))
            {
                item.Item = entity;
                result.Add(item);
            }
        }
        return result;
    }

    public async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await ById<TItem, TParent>(scope, item.Id, transaction, cancellationToken);
    }

    public async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var entity = await ByRef<TItem, TParent>(item, cancellationToken: cancellationToken);
        item.Item = entity;
        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return await All<TItem, TParent>(scope, transaction, cancellationToken);
    }

    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return IQueryable<TItem, TParent>(scope);
    }

    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await One<TItem, TParent>(scope, predicate, sortOrders, transaction, cancellationToken);
    }

    public async Task<TItem> Random<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await Random<TItem, TParent>(scope, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await Random<TItem, TParent>(scope, count, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await Many<TItem, TParent>(scope, predicate, sortOrders, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await Many<TItem, TParent>(scope, whereClause, sortOrders, transaction, cancellationToken);
    }

    async Task<IAsyncEnumerable<TItem>> ITransparentScopedReadonlyRepository.Many<TItem, TParent>(
        Expression<Func<TItem, bool>> predicate,
        int pageSize,
        int pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders,
        IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await Many<TItem, TParent>(scope, predicate, sortOrders, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var query = await Many(whereClause, sortOrders, token: cancellationToken);
        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    public async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.AsQueryable().LongCount(predicate);
    }

    public Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException("Watch is not implemented in StellarRepository.");
    }
}
