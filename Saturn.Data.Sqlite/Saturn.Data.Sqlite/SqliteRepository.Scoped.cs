using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IScopedRepository
{
    public Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedDeleteByIdAsync<TItem>(scope, id, transaction, cancellationToken);

    public Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedDeleteAsync<TItem>(scope, filter, transaction, cancellationToken);

    public Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedDeleteByIdsAsync<TItem>(scope, IDs, transaction, cancellationToken);

    public Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedInsertAsync(scope, entity, transaction, cancellationToken);

    public Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedInsertManyAsync(scope, entities, transaction, cancellationToken);

    public Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedJsonUpdateAsync<TItem>(id, version, json, transaction, cancellationToken);

    public Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedSaveAsync(scope, entity, transaction, cancellationToken);

    public Task Save<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedSaveManyAsync(scope, entities, transaction, cancellationToken);

    public Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedUpdateAsync(scope, entity, transaction, cancellationToken);

    public Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedUpdateWhereAsync(scope, conditionPredicate, entity, transaction, cancellationToken);

    public Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedUpdateManyAsync(scope, entity, transaction, cancellationToken);

    public Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedUpsertAsync(scope, entity, transaction, cancellationToken);

    public Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedUpsertManyAsync(scope, entity, transaction, cancellationToken);
}
