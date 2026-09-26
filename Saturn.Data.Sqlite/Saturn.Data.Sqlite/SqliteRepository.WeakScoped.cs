using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IWeakScopedRepository
{
    public Task Delete<TItem>(string scope, string id, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedDeleteByIdAsync<TItem>(scope, id, transaction, cancellationToken);

    public Task Delete<TItem>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedDeleteAsync<TItem>(scope, filter, transaction, cancellationToken);

    public Task Delete<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedDeleteByIdsAsync<TItem>(scope, IDs, transaction, cancellationToken);

    public Task Insert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedInsertAsync(scope, entity, transaction, cancellationToken);

    public Task Insert<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedInsertManyAsync(scope, entities, transaction, cancellationToken);

    public Task JsonUpdate<TItem>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedJsonUpdateAsync<TItem>(id, version, json, transaction, cancellationToken);

    public Task Save<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedSaveAsync(scope, entity, transaction, cancellationToken);

    public Task Save<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedSaveManyAsync(scope, entities, transaction, cancellationToken);

    public Task Update<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedUpdateAsync(scope, entity, transaction, cancellationToken);

    public Task Update<TItem>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedUpdateWhereAsync(scope, conditionPredicate, entity, transaction, cancellationToken);

    public Task Update<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedUpdateManyAsync(scope, entity, transaction, cancellationToken);

    public Task Upsert<TItem>(string scope, TItem entity, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedUpsertAsync(scope, entity, transaction, cancellationToken);

    public Task Upsert<TItem>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
        => ScopedUpsertManyAsync(scope, entity, transaction, cancellationToken);
}
