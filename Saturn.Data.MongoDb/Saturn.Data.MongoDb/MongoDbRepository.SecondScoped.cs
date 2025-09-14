using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : ISecondScopedRepository
{
    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        await Delete<TItem>(f => f.Scope == Scope.Id && f.SecondScope == secondScope.Id && f.Id == id, transaction, cancellationToken);
    }

    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        await Delete(filter.And(e => e.Scope == Scope.Id && e.SecondScope == secondScope.Id), transaction, cancellationToken);
    }

    public async Task Delete<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        if (!IDs.Any())
        {
            return;
        }

        await ExecuteWithTransaction<TItem>(
            transaction,
            async (collection, session) => await collection.DeleteManyAsync(session, f => f.Scope == Scope.Id && f.SecondScope == secondScope.Id && IDs.Contains(f.Id), cancellationToken: cancellationToken),
            async collection => await collection.DeleteManyAsync(f => f.Scope == Scope.Id && f.SecondScope == secondScope.Id && IDs.Contains(f.Id), cancellationToken: cancellationToken)
        );
    }

    public async Task Insert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope.Id;
        entity.SecondScope = secondScope.Id;
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task Insert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        foreach (var entity in entities)
        {
            entity.Scope = Scope.Id;
            entity.SecondScope = secondScope.Id;
        }

        await Insert(entities, transaction, cancellationToken);
    }

    public async Task JsonUpdate<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        await JsonUpdate<TItem>(id, version, json, transaction, cancellationToken);
    }

    public async Task Save<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope.Id;
        entity.SecondScope = secondScope.Id;
        await Save(entity, transaction, cancellationToken);
    }

    public async Task Save<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        foreach (var entity in entities)
        {
            entity.Scope = Scope.Id;
            entity.SecondScope = secondScope.Id;
        }
        
        await Save(entities, transaction, cancellationToken);
    }

    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope.Id;
        entity.SecondScope = secondScope.Id;
        await Update(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope.Id;
        entity.SecondScope = secondScope.Id;
        var combinedPredicate = conditionPredicate.And(e => e.Scope == Scope.Id && e.SecondScope == secondScope.Id);
        await Update(combinedPredicate, entity, transaction, cancellationToken);
    }

    public async Task Update<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        foreach (var e in entities)
        {
            e.Scope = Scope.Id;
            e.SecondScope = secondScope.Id;
        }
    
        await Update(entities, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        entity.Scope = Scope.Id;
        entity.SecondScope = secondScope.Id;
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        foreach (var e in entities)
        {
            e.Scope = Scope.Id;
            e.SecondScope = secondScope.Id;
        }
        
        await Upsert(entities, transaction, cancellationToken);
    }
}