using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ITransparentScopedReadonlyRepository
{
    async Task<TItem> ITransparentScopedReadonlyRepository.ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken())
    {
        return (await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>()))[id];
    }

    public async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.Where(e => IDs.Contains(e.Id)).ToList();
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> items, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var result = new List<Ref<TItem>>();
        foreach (var item in items)
        {
            if (collection.TryGet(item.Id, out var entity))
            {
                item.Item = entity;
                result.Add(item);
            }
        }
        return result;
    }

    public async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.TryGet(item.Id, out var entity) ? entity : null;
    }

    public async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var entity = await ByRef<TItem, TParent>(item);
        item.Item = entity;
        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.AsEnumerable().Select(kvp => kvp.Value).ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable();
    }

    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.FirstOrDefault();
    }

    public async Task<TItem> Random<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var items = collection.AsEnumerable().ToList();
        return items.Count > 0 ? items[new Random().Next(items.Count)].Value : null;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var items = collection.AsEnumerable().ToList();
        var randomItems = items.OrderBy(_ => Guid.NewGuid()).Take(count).Select(kvp => kvp.Value);
        return randomItems.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable();
    
        foreach (var kvp in whereClause)
        {
            var param = Expression.Parameter(typeof(TItem), "x");
            var property = Expression.PropertyOrField(param, kvp.Key);
            var constant = Expression.Constant(kvp.Value);
            var equal = Expression.Equal(property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, param);
            query = query.Where(lambda);
        }
    
        query = ApplySort(query, sortOrders);
        return query.ToAsyncEnumerable();
    }

    async Task<IAsyncEnumerable<TItem>> ITransparentScopedReadonlyRepository.Many<TItem, TParent>(
        Expression<Func<TItem, bool>> predicate,
        int pageSize,
        int pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders,
        IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var query = await Many(whereClause, sortOrders);
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
