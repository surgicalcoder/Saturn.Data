using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar;

public partial class StellarRepository : IWeakSecondScopedRepository
{
    public async Task Delete<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem>(primaryScope, secondScope, id, transaction, cancellationToken);
        if (entity != null)
        {
            await collection.RemoveAsync(id);
        }
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(filter);
        var items = collection.AsQueryable().Where(combined).Select(r => new EntityId(r.Id)).ToList();
        await collection.RemoveBulkAsync(items);
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combinedScope = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .Compile();

        var validIds = new List<EntityId>();
        foreach (var id in IDs)
        {
            var entity = collection.AsQueryable().FirstOrDefault(e => e.Id == id);
            if (entity != null && combinedScope(entity))
            {
                validIds.Add(new EntityId(id));
            }
        }

        await collection.RemoveBulkAsync(validIds);
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.AddAsync(entity.Id, entity);
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entityDictionary = entity.Select(e =>
            {
                ScopeModelHelper.SetScope(e, primaryScope);
                ScopeModelHelper.SetSecondScope(e, secondScope);
                if (string.IsNullOrWhiteSpace(e.Id))
                {
                    e.Id = EntityId.GenerateNewId();
                }

                return e;
            })
            .ToDictionary(e => string.IsNullOrEmpty(e.Id) ? new EntityId() : new EntityId(e.Id), e => e);
        await collection.AddBulkAsync(entityDictionary);
    }

    public async Task JsonUpdate<TItem>(string primaryScope, string secondScope, string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var entity = await ById<TItem>(primaryScope, secondScope, id, transaction, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with id '{id}' not found.");
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
        ScopeModelHelper.SetScope(updatedEntity, primaryScope);
        ScopeModelHelper.SetSecondScope(updatedEntity, secondScope);

        await collection.UpdateAsync(id, updatedEntity);
    }

    public async Task Save<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        await Upsert(primaryScope, secondScope, entity, transaction, cancellationToken);
    }

    public async Task Save<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        await Upsert(primaryScope, secondScope, entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        await collection.UpdateAsync(entity.Id, entity);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());
        var combined = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope))
            .And(conditionPredicate);
        var itemsToUpdate = collection.AsQueryable().Where(combined).ToList();

        var updateTasks = itemsToUpdate.Select(async item =>
        {
            var updatedEntity = System.Text.Json.JsonSerializer.Deserialize<TItem>(System.Text.Json.JsonSerializer.Serialize(entity));
            ScopeModelHelper.SetScope(updatedEntity, primaryScope);
            ScopeModelHelper.SetSecondScope(updatedEntity, secondScope);
            updatedEntity.Id = item.Id;
            await collection.UpdateAsync(item.Id, updatedEntity);
        });

        await Task.WhenAll(updateTasks);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var collection = await database.GetCollectionAsync<EntityId, TItem>(collectionName: GetCollectionNameForType<TItem>());

        var updateTasks = entity.Select(async e =>
        {
            ScopeModelHelper.SetScope(e, primaryScope);
            ScopeModelHelper.SetSecondScope(e, secondScope);
            await collection.UpdateAsync(e.Id, e);
        });

        await Task.WhenAll(updateTasks);
    }

    public async Task Upsert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        if (entity?.Id == null || string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = EntityId.GenerateNewId();
        }

        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

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

    public async Task Upsert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            await Upsert(primaryScope, secondScope, item, transaction, cancellationToken);
        }
    }
}

