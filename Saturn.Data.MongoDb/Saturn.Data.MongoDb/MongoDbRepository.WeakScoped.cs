using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IWeakScopedRepository
{
    public async Task Delete<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;
        await Delete(scopePredicate.And(idPredicate), transaction, cancellationToken);
    }

    public async Task Delete<TItem>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        await Delete(filter.And(scopePredicate), transaction, cancellationToken);
    }

    public async Task Delete<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var ids = IDs.ToList();

        if (!ids.Any())
        {
            return;
        }

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => ids.Contains(item.Id);

        await Delete(scopePredicate.And(idPredicate), transaction, cancellationToken);
    }

    public async Task Insert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task Insert<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        foreach (var entity in entities)
        {
            ScopeModelHelper.SetScope(entity, scope);
        }

        await Insert(entities, transaction, cancellationToken);
    }

    public async Task JsonUpdate<TItem>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;
        var filter = scopePredicate.And(idPredicate).And(e => (e.Version.HasValue && e.Version <= version) || !e.Version.HasValue);
        var context = BuildWriteContext<TItem>(RepositoryWriteOperation.Patch, id: id, expectedVersion: version, jsonDocument: json, filter: filter,
            transaction: transaction, cancellationToken: cancellationToken);
        await ApplyWriteBehaviors(RepositoryWriteOperation.Patch, context);

        var updateResult = await ExecuteWithTransaction<TItem, UpdateResult>(
            transaction,
            (collection, session) => collection.UpdateOneAsync(session, filter, new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken),
            collection => collection.UpdateOneAsync(filter, new JsonUpdateDefinition<TItem>(json), cancellationToken: cancellationToken)
        );

        if (!updateResult.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Save<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Save(entity, transaction, cancellationToken);
    }

    public async Task Save<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        foreach (var entity in entities)
        {
            ScopeModelHelper.SetScope(entity, scope);
        }

        await Save(entities, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Update(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        await Update(conditionPredicate.And(scopePredicate), entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        foreach (var entity in entities)
        {
            ScopeModelHelper.SetScope(entity, scope);
        }

        await Update(entities, transaction, cancellationToken);
    }

    public async Task Upsert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, scope);
        }

        await Upsert(entity, transaction, cancellationToken);
    }
}

