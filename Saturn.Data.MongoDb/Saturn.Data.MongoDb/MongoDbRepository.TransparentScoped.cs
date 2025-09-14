using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : ITransparentScopedRepository
{
    public async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete(filter.And(e => e.Scope == scope), transaction, cancellationToken);
    }

    public async Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem>(f => f.Scope == scope && f.Id == id, transaction, cancellationToken);
    }

    public async Task Delete<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {        
        if (!IDs.Any())
        {
            return;
        }
        
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        await ExecuteWithTransaction<TItem>(
            transaction,
            async (collection, session) => await collection.DeleteManyAsync(session, f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken),
            async collection => await collection.DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken)
        );
    }

    public async Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task Insert<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        foreach (var entity in entities)
        {
            entity.Scope = scope;
        }

        await Insert(entities, transaction, cancellationToken);
    }

    public async Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await JsonUpdate<TItem, TParent>(scope, id, version, json, transaction, cancellationToken);
    }

    public async Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Save(entity, transaction, cancellationToken);
    }

    public async Task Save<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        foreach (var entity in entities)
        {
            entity.Scope = scope;
        }
        
        await Save(entities, transaction, cancellationToken);
    }

    public async Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Update(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TParent>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        var combinedPredicate = conditionPredicate.And(e => e.Scope == scope);
        await Update(combinedPredicate, entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        foreach (var e in entities)
        {
            e.Scope = scope;
        }
    
        await Update(entities, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        foreach (var e in entities)
        {
            e.Scope = scope;
        }
        
        await Upsert(entities, transaction, cancellationToken);
    }
}