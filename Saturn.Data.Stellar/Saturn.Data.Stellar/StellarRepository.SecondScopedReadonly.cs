using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ISecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await All<TItem, TSecondScope, TScope>(Scope, secondScope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return null;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }
    
    public async Task<TItem> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await ById<TItem, TSecondScope, TScope>(Scope, secondScope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
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
            if (!includeDeleted && SupportsSoftDelete<TItem>() && item is ISoftDeletable { IsDeleted: true })
            {
                return null;
            }

            return item;
        }

        return null;
    }
    
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await ById<TItem, TSecondScope, TScope>(Scope, secondScope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return null;
        }
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(e => IDs.Contains(e.Id) && e.Scope == Scope && e.SecondScope == secondScope);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);

        return query.ToAsyncEnumerable();
    }
    
    public async Task<long> Count<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await Count<TItem, TSecondScope, TScope>(Scope, secondScope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (Scope == null || secondScope == null)
        {
            return 0;
        }

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = ApplySoftDeleteFilter<TItem>(scopePred.And(predicate), includeDeleted);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        return query.LongCount();
    }
    
    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return IQueryable<TItem, TSecondScope, TScope>(Scope, secondScope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, bool includeDeleted) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        var query = collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope);
        return ApplySoftDeleteFilter<TItem>(query, includeDeleted);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await Many<TItem, TSecondScope, TScope>(Scope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = ApplySoftDeleteFilter<TItem>(scopePred.And(predicate), includeDeleted);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable<TItem>();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await Many<TItem, TSecondScope, TScope>(Scope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable(), includeDeleted);
        
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
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        
        return query.ToAsyncEnumerable<TItem>();
    }
    
    public async Task<TItem> One<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await One<TItem, TSecondScope, TScope>(Scope, secondScope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = ApplySoftDeleteFilter<TItem>(scopePred.And(predicate), includeDeleted);
        var query = collection.AsQueryable().Where(combinedPred);
        
        query = ApplySort(query, sortOrders);
        
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        
        return query.FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await Random<TItem, TSecondScope, TScope>(Scope, secondScope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope && item.SecondScope == secondScope;
        var combinedPred = predicate != null ? scopePred.And(predicate) : scopePred;
        combinedPred = ApplySoftDeleteFilter<TItem>(combinedPred, includeDeleted);
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