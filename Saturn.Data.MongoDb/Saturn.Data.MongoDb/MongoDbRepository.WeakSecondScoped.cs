using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IWeakSecondScopedRepository
{
    public async Task Delete<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;

        await Delete(scopePredicate.And(idPredicate), transaction, cancellationToken);
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));

        await Delete(filter.And(scopePredicate), transaction, cancellationToken);
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var ids = IDs.ToList();

        if (!ids.Any())
        {
            return;
        }

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        Expression<Func<TItem, bool>> idPredicate = item => ids.Contains(item.Id);

        await Delete(scopePredicate.And(idPredicate), transaction, cancellationToken);
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);
        }

        await Insert(entity, transaction, cancellationToken);
    }

    public async Task JsonUpdate<TItem>(string primaryScope, string secondScope, string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task Save<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Save(entity, transaction, cancellationToken);
    }

    public async Task Save<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);
        }

        await Save(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Update(entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        await Update(conditionPredicate.And(scopePredicate), entity, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);
        }

        await Update(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task Upsert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);
        }

        await Upsert(entity, transaction, cancellationToken);
    }
}

