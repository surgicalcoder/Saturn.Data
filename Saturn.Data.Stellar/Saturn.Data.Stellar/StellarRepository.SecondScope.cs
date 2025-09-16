using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : ISecondScopedRepository
{
    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem, TSecondScope, TScope>(Scope, secondScope, id);
        if (entity != null && entity.Scope == Scope && entity.SecondScope == secondScope)
        {
            await collection.RemoveAsync(id);
        }
    }
    
    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var items = collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope).Where(filter).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }
    
    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var validIds = new List<EntityId>();
        
        foreach (var id in IDs)
        {
            var entity = collection.AsQueryable().FirstOrDefault(e => e.Id == id && e.Scope == Scope && e.SecondScope == secondScope);
            if (entity != null)
            {
                validIds.Add(new EntityId(id));
            }
        }
        
        await collection.RemoveBulkAsync(validIds);
    }
    
    public async Task Insert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope;
        entity.SecondScope = secondScope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }
    
    public async Task Insert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entity.Select(e => { e.Scope = Scope; e.SecondScope = secondScope; return e; })
            .ToDictionary(e => string.IsNullOrEmpty(e.Id) ? new EntityId() : new EntityId(e.Id), e => e);
        await collection.AddBulkAsync(entityDictionary);
    }
    
    public async Task JsonUpdate<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityId = new EntityId(id);
        var entity = await ById<TItem, TSecondScope, TScope>(Scope, secondScope, id, cancellationToken: cancellationToken);
    
        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with id '{id}' not found.");
        }
    
        if (entity.Scope != Scope || entity.SecondScope != secondScope)
        {
            throw new InvalidOperationException("Scope mismatch.");
        }
    
        if (entity.Version != version)
        {
            throw new InvalidOperationException($"Version mismatch: expected {entity.Version}, got {version}.");
        }
    
        var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(json);
        if (updatedEntity == null)
        {
            throw new InvalidOperationException("Deserialization failed.");
        }
    
        updatedEntity.Id = id;
        updatedEntity.Scope = Scope;
        updatedEntity.SecondScope = secondScope;
    
        await collection.UpdateAsync(id, updatedEntity);
    }
    
    public async Task Save<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        await Upsert<TItem, TSecondScope, TScope>(Scope, secondScope, entity, transaction, cancellationToken);
    }
    
    public async Task Save<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        await Upsert<TItem, TSecondScope, TScope>(Scope, secondScope, entity, transaction, cancellationToken);
    }
    
    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope;
        entity.SecondScope = secondScope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }
    
    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var itemsToUpdate = collection.AsQueryable().Where(e => e.Scope == Scope && e.SecondScope == secondScope).Where(conditionPredicate).ToList();
        
        var updateTasks = itemsToUpdate.Select(async item =>
        {
            var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(System.Text.Json.JsonSerializer.Serialize(entity));
            updatedEntity.Scope = Scope;
            updatedEntity.SecondScope = secondScope;
            updatedEntity.Id = item.Id;
            await collection.UpdateAsync(item.Id, updatedEntity);
        });
        
        await Task.WhenAll(updateTasks);
    }
    
    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        
        var updateTasks = entity.Select(async e =>
        {
            e.Scope = Scope;
            e.SecondScope = secondScope;
            await collection.UpdateAsync(e.Id, e);
        });
        
        await Task.WhenAll(updateTasks);
    }
    
    public async Task Upsert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope;
        entity.SecondScope = secondScope;
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        if (collection.ContainsKey(entity.Id))
        {
            await collection.UpdateAsync(entity.Id, entity);
        }
        else
        {
            await collection.AddAsync(entity.Id, entity);
        }
    }
    
    public async Task Upsert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        foreach (var item in entity)
        {
            await Upsert<TItem, TSecondScope, TScope>(Scope, secondScope, item, transaction, cancellationToken);
        }
    }
}
