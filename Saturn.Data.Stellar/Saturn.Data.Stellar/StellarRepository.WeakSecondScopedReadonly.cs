using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IWeakSecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return collection.AsQueryable().Where(combined).ToAsyncEnumerable();
    }

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (!collection.ContainsKey(id))
        {
            return null;
        }

        var item = collection[id];
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return combined.Compile()(item) ? item : null;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return collection.AsQueryable().Where(e => IDs.Contains(e.Id)).Where(combined).ToAsyncEnumerable();
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        var query = collection.AsQueryable().Where(combined);
        query = ApplyContinueFrom(query, continueFrom);
        return query.LongCount();
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return collection.AsQueryable().Where(combined);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        var query = collection.AsQueryable().Where(combined);
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom(query, continueFrom);
        query = ApplyPaging(query, pageSize, pageNumber);
        return query.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var query = collection.AsQueryable().Where(combined);

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

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(predicate);
        var query = collection.AsQueryable().Where(combined);
        query = ApplySort(query, sortOrders);
        query = ApplyContinueFrom(query, continueFrom);
        return query.FirstOrDefault();
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var combined = predicate != null ? scopePredicate.And(predicate) : scopePredicate;
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

