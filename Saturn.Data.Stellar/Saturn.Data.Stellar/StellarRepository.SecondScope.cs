using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ISecondScopedRepository
{
    public async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.AsQueryable().FirstOrDefault(e => e.Id == id && e.SecondScope.Equals(secondScope) == true && e.Scope.Equals(primaryScope) == true);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.AsQueryable().Where(e => e.SecondScope.Equals(secondScope) == true && e.Scope.Equals(primaryScope) == true).ToAsyncEnumerable();
    }
    
    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable().Where(e => e.SecondScope.Equals(secondScope) == true && e.Scope.Equals(primaryScope) == true);
    }

    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(e => e.SecondScope.Equals(secondScope) == true && e.Scope.Equals(primaryScope) == true).Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.FirstOrDefault();
    }

    async Task<IAsyncEnumerable<TItem>> ISecondScopedRepository.Many<TItem, TSecondScope, TPrimaryScope>(
        Ref<TPrimaryScope> primaryScope,
        Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate,
        int pageSize,
        int pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders,
        IDatabaseTransaction transaction,
        CancellationToken cancellationToken)
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable()
            .Where(e => e.SecondScope.Equals(secondScope) && e.Scope.Equals(primaryScope))
            .Where(predicate);
    
        query = ApplySort(query, sortOrders);
    
        var pagedQuery = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    
        return pagedQuery.ToAsyncEnumerable();
    }

    public async Task<long> CountMany<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        return collection.AsQueryable().Where(e => e.SecondScope.Equals(secondScope) == true && e.Scope.Equals(primaryScope) == true).LongCount(predicate);
    }

    public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.SecondScope = secondScope;
        entity.Scope = primaryScope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.SecondScope = secondScope;
        entity.Scope = primaryScope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }

    public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope;
        entity.SecondScope = secondScope;

        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        if (collection.ContainsKey(entity.Id))
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
        else
        {
            await collection.AddAsync(entity.Id, entity);
        }
    }

    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string Id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var entity = await ById<TItem, TSecondScope, TPrimaryScope>(primaryScope, secondScope, Id);
        if (entity != null && entity.Scope.Equals(primaryScope) && entity.SecondScope.Equals(secondScope))
        {
            await collection.RemoveAsync(Id);
        }
    }
}
