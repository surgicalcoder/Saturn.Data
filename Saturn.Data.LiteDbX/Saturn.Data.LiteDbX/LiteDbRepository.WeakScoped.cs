using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IWeakScopedRepository
{
    public async Task Delete<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var matchingEntities = await ById<TItem>(scope, IDs, transaction, cancellationToken);

        await foreach (var entity in matchingEntities.WithCancellation(cancellationToken))
        {
            await GetCollection<TItem>().Delete(entity.Id, cancellationToken);
        }
    }

    public async Task Insert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().Insert(entity, cancellationToken);
    }

    public async Task Insert<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        foreach (var entity in entities)
        {
            ScopeModelHelper.SetScope(entity, scope);
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = ObjectId.NewObjectId().ToString();
            }
        }

        await GetCollection<TItem>().Insert(entities, cancellationToken);
    }

    public async Task JsonUpdate<TItem>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var collection = GetCollection<TItem>();
        var entity = await collection.FindOne(scopePredicate.And(e => e.Id == id), cancellationToken);

        if (entity == null)
        {
            throw new NotSupportedException("Entity not found");
        }

        entity = System.Text.Json.JsonSerializer.Deserialize<TItem>(json);
        entity.Version = version;

        var updateResult = await collection.Update(entity, cancellationToken);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Save<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public async Task Save<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        foreach (var entity in entities)
        {
            ScopeModelHelper.SetScope(entity, scope);
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = ObjectId.NewObjectId().ToString();
            }
        }

        await Upsert(entities, transaction, cancellationToken);
    }

    public async Task Update<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Update(entity, cancellationToken: cancellationToken);
    }

    public async Task Update<TItem>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        await Update(conditionPredicate.And(scopePredicate), entity, cancellationToken: cancellationToken);
    }

    public async Task Update<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        foreach (var scopedEntity in entity)
        {
            ScopeModelHelper.SetScope(scopedEntity, scope);
        }

        await Update(entity, cancellationToken: cancellationToken);
    }

    public async Task Upsert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public async Task Upsert<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, scope);
        }

        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public async Task Delete<TItem>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        await Delete(filter.And(scopePredicate), transaction, cancellationToken);
    }

    public async Task Delete<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        await Delete(scopePredicate.And(f => f.Id == id), cancellationToken: cancellationToken);
    }
}
