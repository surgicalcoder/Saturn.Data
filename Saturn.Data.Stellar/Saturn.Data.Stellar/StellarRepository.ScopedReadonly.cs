using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository: IScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }
        
        var item = collection[id];

        if (item.Scope == scope)
        {
            return item;
        }

        return null;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        
        return collection.AsQueryable().Where(e => IDs.Contains(e.Id) && e.Scope.Equals(scope)).ToAsyncEnumerable();
    }


    public virtual async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        return collection.AsQueryable().Where(e => e.Scope == scope).ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable().Where(e => e.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == scope;
        var combinedPred = scopePred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        // Apply sorting first to establish the correct order
        query = ApplySort(query, sortOrders);
        
        // Then apply continuation filter based on that order
        query = ApplyContinueFrom(query, continueFrom);
        
        // Finally apply pagination
        query = ApplyPaging(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable();
        
        // Apply scope filter first
        query = query.Where(e => e.Scope == scope);
        
        // Apply where clause filters
        foreach (var kvp in whereClause)
        {
            if (kvp.Key == "Scope") continue; // Skip scope as we already applied it
            
            var parameter = Expression.Parameter(typeof(TItem), "x");
            var property = Expression.PropertyOrField(parameter, kvp.Key);
            var constant = Expression.Constant(kvp.Value);
            var equal = Expression.Equal(property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, parameter);
            query = query.Where(lambda);
        }
        
        // Apply sorting first to establish the correct order
        query = ApplySort(query, sortOrders);
        
        // Then apply continuation filter based on that order
        query = ApplyContinueFrom(query, continueFrom);
        
        // Finally apply pagination
        query = ApplyPaging(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == scope;
        var combinedPred = scopePred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        // Apply sorting first to establish the correct order
        query = ApplySort(query, sortOrders);
        
        // Then apply continuation filter based on that order
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == scope;
        var combinedPred = predicate != null ? scopePred.And(predicate) : scopePred;
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        var filteredItems = query.ToList();
        
        if (filteredItems.Count == 0)
            return AsyncEnumerable.Empty<TItem>();
        
        var random = new Random();
        var selectedItems = new List<TItem>();
        var actualCount = Math.Min(count, filteredItems.Count);
        
        for (int i = 0; i < actualCount; i++)
        {
            var randomIndex = random.Next(filteredItems.Count);
            selectedItems.Add(filteredItems[randomIndex]);
            filteredItems.RemoveAt(randomIndex);
        }
        
        return selectedItems.ToAsyncEnumerable();
    }

    public virtual async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return 0;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.LongCount();
    }
}