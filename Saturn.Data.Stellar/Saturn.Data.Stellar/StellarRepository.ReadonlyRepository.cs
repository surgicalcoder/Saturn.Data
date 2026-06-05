using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        return await ById<TItem>(id, includeDeleted: false, transaction, token);
    }

    public async Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        if (!collection.ContainsKey(id))
        {
            return null;
        }

        var item = collection[id];

        if (!includeDeleted && SupportsSoftDelete<TItem>() && item is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(e => IDs.Contains(e.Id));
        query = ApplySoftDeleteFilter(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        return await All<TItem>(includeDeleted: false, transaction, token);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable(), includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return IQueryable<TItem>(includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        return ApplySoftDeleteFilter<TItem>(collection.AsQueryable(), includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(predicate, includeDeleted));
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable<TItem>();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable(), includeDeleted);
        
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
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable<TItem>();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await One(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(predicate, includeDeleted));
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        return query.FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable(), includeDeleted);
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        var items = query.ToList();
        var randomItems = items.OrderBy(_ => Guid.NewGuid()).Take(count);
        
        return randomItems.ToAsyncEnumerable();
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        return await Count(predicate, continueFrom, includeDeleted: false, transaction, token);
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(predicate, includeDeleted));
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        return query.LongCount();
    }
}
