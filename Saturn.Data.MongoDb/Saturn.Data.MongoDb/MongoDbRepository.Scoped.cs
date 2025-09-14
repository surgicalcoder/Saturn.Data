using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IScopedRepository
{
    public async Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await Delete<TItem>(f => f.Scope == scope && f.Id == id, transaction, cancellationToken);
    }

    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await Delete(filter.And(e => e.Scope == scope), transaction, cancellationToken);
    }

    public async Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (!IDs.Any())
        {
            return;
        }

        await ExecuteWithTransaction<TItem>(
            transaction,
            async (collection, session) => await collection.DeleteManyAsync(session, f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken),
            async collection => await collection.DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken)
        );
    }

    public async Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;
        }

        await Insert(entities, transaction, cancellationToken);
    }

    public async Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await JsonUpdate<TItem>(id, version, json, transaction, cancellationToken);
    }

    public async Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Save(entity, transaction, cancellationToken);
    }

    public async Task Save<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var entity in entities)
        {
            entity.Scope = scope;
        }
        await Save(entities, transaction, cancellationToken);
    }

    public async Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Update(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        var combinedPredicate = conditionPredicate.And(e => e.Scope == scope);
        await Update(combinedPredicate, entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var e in entities)
        {
            e.Scope = scope;
        }
    
        await Update(entities, transaction, cancellationToken);
    }
    
    public async Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var e in entities)
        {
            e.Scope = scope;
        }
        await Upsert(entities, transaction, cancellationToken);
    }
}