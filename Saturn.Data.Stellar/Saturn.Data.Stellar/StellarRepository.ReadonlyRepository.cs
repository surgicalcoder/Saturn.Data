using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        return (await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()))[id];
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return (await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>())).Where(e=> IDs.Contains(e.Id)).ToAsyncEnumerable();
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem>(List<Ref<TItem>> items, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
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

    public async Task<TItem> ByRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.TryGet(item.Id, out var entity) ? entity : null;
    }

    public async Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity, new()
    {
        if (item == null || string.IsNullOrEmpty(item.Id))
        {
            return item;
        }
        
        item.Item = await ById<TItem>(item.Id, transaction, cancellationToken);

        return item;
    }

    public async Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item) where TItem : Entity, new()
    {
        var entity = await ByRef(item);
        item.Item = entity;
        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsEnumerable().Select(kvp => kvp.Value).ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable();
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
    
        query = ApplySort(query, sortOrders);
    
        return query.FirstOrDefault();
    }

    public async Task<TItem> Random<TItem>(IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsEnumerable().ToList();
        return items.Count > 0 ? items[new Random().Next(items.Count)].Value : null;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(int count, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsEnumerable().ToList();
        var randomItems = items.OrderBy(_ => Guid.NewGuid()).Take(count).Select(kvp => kvp.Value);
        return randomItems.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
    
        query = ApplySort(query, sortOrders);
    
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        throw new NotImplementedException("Unsupported");
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var query = await Many(predicate, sortOrders, token: token);
        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var query = await Many(whereClause, sortOrders, token: token);
        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    public async Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsQueryable().LongCount(predicate);
    }

    public async Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        throw new NotImplementedException("Watch is not implemented in StellarRepository.");
    }
}
