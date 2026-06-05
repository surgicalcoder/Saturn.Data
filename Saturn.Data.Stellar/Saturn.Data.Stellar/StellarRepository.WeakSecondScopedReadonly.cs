using System.Linq.Expressions;
using FastExpressionCompiler;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IWeakSecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await All<TItem>(primaryScope, secondScope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var query = collection.AsQueryable().Where(combined);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await ById<TItem>(primaryScope, secondScope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }

        var item = collection[id];
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        if (!combined.CompileFast()(item))
        {
            return null;
        }

        if (!includeDeleted && SupportsSoftDelete<TItem>() && item is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await ById<TItem>(primaryScope, secondScope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var query = collection.AsQueryable().Where(e => IDs.Contains(e.Id)).Where(combined);
        query = ApplySoftDeleteFilter<TItem>(query, includeDeleted);
        return query.ToAsyncEnumerable();
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await Count<TItem>(primaryScope, secondScope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        combined = ApplySoftDeleteFilter<TItem>(combined, includeDeleted);
        var query = collection.AsQueryable().Where(combined);
        query = ApplyContinueFrom(query, continueFrom);
        return query.LongCount();
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope) where TItem : Entity, ISecondScopedById, new()
    {
        return IQueryable<TItem>(primaryScope, secondScope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var query = collection.AsQueryable().Where(combined);
        return ApplySoftDeleteFilter<TItem>(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        combined = ApplySoftDeleteFilter<TItem>(combined, includeDeleted);
        var query = collection.AsQueryable().Where(combined);
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom<TItem>(query, continueFrom);
        query = ApplyPaging<TItem>(query, pageSize, pageNumber);
        return query.ToAsyncEnumerable<TItem>();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var query = ApplySoftDeleteFilter<TItem>(collection.AsQueryable().Where(combined), includeDeleted);

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

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await One<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        combined = ApplySoftDeleteFilter<TItem>(combined, includeDeleted);
        var query = collection.AsQueryable().Where(combined);
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom(query, continueFrom);
        return query.FirstOrDefault();
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        return await Random<TItem>(primaryScope, secondScope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var combined = predicate != null ? scopePredicate.And(predicate) : scopePredicate;
        combined = ApplySoftDeleteFilter<TItem>(combined, includeDeleted);
        var query = collection.AsQueryable().Where(combined);

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
}

