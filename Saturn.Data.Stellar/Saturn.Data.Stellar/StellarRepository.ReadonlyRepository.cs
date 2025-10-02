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

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        query = ApplyPaging(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable();
        
        // Apply where clause filters
        foreach (var kvp in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "x");
            var property = Expression.PropertyOrField(parameter, kvp.Key);
            var constant = Expression.Constant(kvp.Value);
            var equal = Expression.Equal(property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, parameter);
            query = query.Where(lambda);
        }
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        query = ApplyPaging(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        query = ApplyContinueFrom(query, continueFrom);
        
        var items = query.ToList();
        var randomItems = items.OrderBy(_ => Guid.NewGuid()).Take(count);
        
        return randomItems.ToAsyncEnumerable();
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.LongCount();
    }
}
