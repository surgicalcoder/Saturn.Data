using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : ISecondScopedRepository
{
    public Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedDeleteByIdAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, id, transaction, cancellationToken);

    public Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedDeleteAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, filter, transaction, cancellationToken);

    public Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedDeleteByIdsAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, IDs, transaction, cancellationToken);

    public Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedInsertAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedInsertManyAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task JsonUpdate<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, int version,
        string json, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => ScopedJsonUpdateAsync<TItem>(id, version, json, transaction, cancellationToken);

    public Task Save<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedSaveAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Save<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedSaveManyAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedUpdateAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedUpdateWhereAsync(primaryScope?.Id!, secondScope?.Id!, conditionPredicate, entity, transaction, cancellationToken);

    public Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedUpdateManyAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedUpsertAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);

    public Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedUpsertManyAsync(primaryScope?.Id!, secondScope?.Id!, entity, transaction, cancellationToken);
}
