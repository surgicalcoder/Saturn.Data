using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TScope>(TScope scope, string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsQueryable().Where(e=> e.Id == id && e.Scope == scope).FirstOrDefault();
    }
    
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(TScope scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entities = IDs.Select(id => new EntityId(id));
        return collection.AsQueryable().Where(e => entities.Contains(e.Id)).ToAsyncEnumerable();
    }
    
    public async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsQueryable().FirstOrDefault(e => e.Id == id && e.Scope == scope);
    }
    
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entities = IDs.Select(id => new EntityId(id));
        return collection.AsQueryable().Where(e => entities.Contains(e.Id)).ToAsyncEnumerable();
    }
    
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        return collection.AsQueryable().Where(e => e.Scope == scope).ToAsyncEnumerable();
    }
    
    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>()).Result;
        return collection.AsQueryable();
    }
    
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.FirstOrDefault();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(GetCollectionNameForType<TItem>());
        var query = collection.AsQueryable().Where(predicate);
        query = ApplySort(query, sortOrders);
        return query.ToAsyncEnumerable();
    }

    public async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken token = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        return collection.AsQueryable().LongCount(combinedPred);
    }
}
