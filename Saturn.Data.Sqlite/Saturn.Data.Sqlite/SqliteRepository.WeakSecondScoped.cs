using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IWeakSecondScopedRepository
{
    public Task Delete<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedDeleteByIdAsync<TItem>(primaryScope, secondScope, id, transaction, cancellationToken);

    public Task Delete<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedDeleteAsync<TItem>(primaryScope, secondScope, filter, transaction, cancellationToken);

    public Task Delete<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedDeleteByIdsAsync<TItem>(primaryScope, secondScope, IDs, transaction, cancellationToken);

    public Task Insert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedInsertAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Insert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedInsertManyAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task JsonUpdate<TItem>(string primaryScope, string secondScope, string id, int version, string json,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => ScopedJsonUpdateAsync<TItem>(id, version, json, transaction, cancellationToken);

    public Task Save<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedSaveAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Save<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedSaveManyAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Update<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedUpdateAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Update<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedUpdateWhereAsync(primaryScope, secondScope, conditionPredicate, entity, transaction, cancellationToken);

    public Task Update<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedUpdateManyAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Upsert<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedUpsertAsync(primaryScope, secondScope, entity, transaction, cancellationToken);

    public Task Upsert<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedUpsertManyAsync(primaryScope, secondScope, entity, transaction, cancellationToken);
}
