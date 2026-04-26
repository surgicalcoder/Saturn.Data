using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IWeakSecondScopedRepository
{
    public async Task Delete<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        await GetCollection<TItem>().DeleteMany(scopePredicate.And(f => f.Id == id), cancellationToken);
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        await GetCollection<TItem>().DeleteMany(filter.And(scopePredicate), cancellationToken);
    }

    public async Task Delete<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var matchingEntities = await ById<TItem>(primaryScope, secondScope, IDs, transaction, cancellationToken);

        await foreach (var entity in matchingEntities.WithCancellation(cancellationToken))
        {
            await GetCollection<TItem>().Delete(entity.Id, cancellationToken);
        }
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().Insert(entity, cancellationToken);
    }

    public async Task Insert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = ObjectId.NewObjectId().ToString();
            }
        }

        await GetCollection<TItem>().Insert(entity, cancellationToken);
    }

    public async Task JsonUpdate<TItem>(string primaryScope, string secondScope, string id, int version, string json, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));

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

    public async Task Save<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().Upsert(entity, cancellationToken);
    }

    public async Task Save<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = ObjectId.NewObjectId().ToString();
            }
        }

        await GetCollection<TItem>().Upsert(entity, cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Update(entity, cancellationToken: cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        await Update(conditionPredicate.And(scopePredicate), entity, cancellationToken: cancellationToken);
    }

    public async Task Update<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);
        }

        await Update(entity, cancellationToken: cancellationToken);
    }

    public async Task Upsert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().Upsert(entity, cancellationToken);
    }

    public async Task Upsert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity, ISecondScopedById, new()
    {
        foreach (var item in entity)
        {
            ScopeModelHelper.SetScope(item, primaryScope);
            ScopeModelHelper.SetSecondScope(item, secondScope);

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = ObjectId.NewObjectId().ToString();
            }
        }

        await GetCollection<TItem>().Upsert(entity, cancellationToken);
    }
}
