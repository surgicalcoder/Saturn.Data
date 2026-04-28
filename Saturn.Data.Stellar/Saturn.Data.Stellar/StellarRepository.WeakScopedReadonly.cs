using System.Linq.Expressions;
using FastExpressionCompiler;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IWeakScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }

        var item = collection[id];
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return scopePredicate.CompileFast()(item) ? item : null;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return collection.AsQueryable().Where(e => IDs.Contains(e.Id)).Where(scopePredicate).ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return collection.AsQueryable().Where(scopePredicate).ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return collection.AsQueryable().Where(scopePredicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate.And(predicate));
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom(query, continueFrom);
        query = ApplyPaging(query, pageSize, pageNumber);
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate);

        foreach (var kvp in whereClause)
        {
            if (kvp.Key == "Scope") continue;
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

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate.And(predicate));
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom(query, continueFrom);
        return query.FirstOrDefault();
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = predicate != null ? scopePredicate.And(predicate) : scopePredicate;
        var query = collection.AsQueryable().Where(combinedPredicate);

        query = ApplyContinueFrom(query, continueFrom);

        var filteredItems = query.ToList();
        if (filteredItems.Count == 0)
        {
            return AsyncEnumerable.Empty<TItem>();
        }

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

    public async Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate.And(predicate));
        query = ApplyContinueFrom(query, continueFrom);
        return query.LongCount();
    }
}

