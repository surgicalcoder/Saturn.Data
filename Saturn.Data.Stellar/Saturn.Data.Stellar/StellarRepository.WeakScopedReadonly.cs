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
        return await ById<TItem>(scope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }

        var item = collection[id];
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        if (!scopePredicate.CompileFast()(item))
        {
            return null;
        }

        if (!includeDeleted && SupportsSoftDelete<TItem>() && item is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await ById<TItem>(scope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(e => IDs.Contains(e.Id)).Where(scopePredicate);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await All<TItem>(scope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new()
    {
        return IQueryable<TItem>(scope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope, bool includeDeleted)
        where TItem : Entity, IScopedById, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(scopePredicate);
        return ApplySoftDeleteFilter<TItem>(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await Many<TItem>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(scopePredicate.And(predicate), includeDeleted));
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        return query.ToAsyncEnumerable<TItem>();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await Many<TItem>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable().Where(scopePredicate), includeDeleted);

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
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        return query.ToAsyncEnumerable<TItem>();
    }

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await One<TItem>(scope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(scopePredicate.And(predicate), includeDeleted));
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        return query.FirstOrDefault();
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        return await Random<TItem>(scope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = predicate != null ? scopePredicate.And(predicate) : scopePredicate;
        combinedPredicate = ApplySoftDeleteFilter<TItem>(combinedPredicate, includeDeleted);
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
        return await Count<TItem>(scope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var query = collection.AsQueryable().Where(ApplySoftDeleteFilter<TItem>(scopePredicate.And(predicate), includeDeleted));
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        return query.LongCount();
    }
}

