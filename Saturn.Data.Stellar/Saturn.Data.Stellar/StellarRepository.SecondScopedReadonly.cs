using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ISecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return null;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope).ToAsyncEnumerable();
    }
    
    public async Task<TItem> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }
        
        var item = collection[id];
        if (item.Scope == Scope && item.SecondScope == secondScope)
        {
            return item;
        }

        return null;
    }
    
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return null;
        }
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        
        return collection.AsQueryable().Where(e => IDs.Contains(e.Id) && e.Scope == Scope && e.SecondScope == secondScope).ToAsyncEnumerable();
    }
    
    public async Task<long> Count<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return 0;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = scopePred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.LongCount();
    }
    
    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = scopePred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        query = ApplyPaging(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable();
        
        query = query.Where(e => e.Scope == Scope && e.SecondScope == secondScope);
        
        foreach (var kvp in whereClause)
        {
            if (kvp.Key is "Scope" or "SecondScope") continue;
            
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
    
    public async Task<TItem> One<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = scopePred.And(predicate);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom(query, continueFrom);
        
        return query.FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
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
}