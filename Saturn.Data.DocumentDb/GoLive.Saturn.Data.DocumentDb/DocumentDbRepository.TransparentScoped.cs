using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : ITransparentScopedRepository
{
    public Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedDeleteAsync<TItem>(ResolveTransparentScope<TParent>(), filter, transaction, cancellationToken);

    public Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedDeleteByIdAsync<TItem>(ResolveTransparentScope<TParent>(), id, transaction, cancellationToken);

    public Task Delete<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedDeleteByIdsAsync<TItem>(ResolveTransparentScope<TParent>(), IDs, transaction, cancellationToken);

    public Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedInsertAsync(ResolveTransparentScope<TParent>(), entity, transaction, cancellationToken);

    public Task Insert<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedInsertManyAsync(ResolveTransparentScope<TParent>(), entities, transaction, cancellationToken);

    public Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedJsonUpdateAsync<TItem>(id, version, json, transaction, cancellationToken);

    public Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedSaveAsync(ResolveTransparentScope<TParent>(), entity, transaction, cancellationToken);

    public Task Save<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedSaveManyAsync(ResolveTransparentScope<TParent>(), entities, transaction, cancellationToken);

    public Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedUpdateAsync(ResolveTransparentScope<TParent>(), entity, transaction, cancellationToken);

    public Task Update<TItem, TParent>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedUpdateWhereAsync(ResolveTransparentScope<TParent>(), conditionPredicate, entity, transaction, cancellationToken);

    public Task Update<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedUpdateManyAsync(ResolveTransparentScope<TParent>(), entities, transaction, cancellationToken);

    public Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedUpsertAsync(ResolveTransparentScope<TParent>(), entity, transaction, cancellationToken);

    public Task Upsert<TItem, TParent>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedUpsertManyAsync(ResolveTransparentScope<TParent>(), entity, transaction, cancellationToken);
}
