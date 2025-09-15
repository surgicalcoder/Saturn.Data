using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository //: ITransparentScopedRepository
{
    public virtual async Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Insert<TItem, TParent>(scope, entity, cancellationToken: cancellationToken);
    }

    public virtual async Task InsertMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await InsertMany<TItem, TParent>(scope, entities, cancellationToken: cancellationToken);
    }

    public virtual async Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Upsert<TItem, TParent>(entity, cancellationToken: cancellationToken);
    }

    public virtual async Task SaveMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpsertMany<TItem, TParent>(scope, entities, cancellationToken: cancellationToken);
    }

    public virtual async Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, entity, cancellationToken: cancellationToken);
    }

    public virtual async Task UpdateMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entities, cancellationToken: cancellationToken);
    }

    public virtual async Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Upsert<TItem, TParent>(scope, entity, cancellationToken: cancellationToken);
    }

    public virtual async Task UpsertMany<TItem, TParent>(List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entity, cancellationToken: cancellationToken);
    }

    public virtual async Task Delete<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, entity, cancellationToken: cancellationToken);
    }

    public virtual async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, filter, cancellationToken: cancellationToken);
    }

    public virtual async Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, id, cancellationToken: cancellationToken);
    }

    public virtual async Task DeleteMany<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, IDs, cancellationToken: cancellationToken);
    }

    public virtual async Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await JsonUpdate<TItem, TParent>(scope, id, version, json, cancellationToken: cancellationToken);
    }


    public virtual async Task DeleteMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, entities.Select(r => r.Id).ToList(), cancellationToken: cancellationToken);
    }
}