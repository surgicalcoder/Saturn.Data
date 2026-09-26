using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository
{
    private static Expression<Func<TItem, bool>> CombineScope<TItem>(string scope, Expression<Func<TItem, bool>>? predicate)
        where TItem : Entity, IScopedById
        => predicate is null
            ? ScopeModelHelper.BuildScopePredicate<TItem>(scope)
            : ScopeModelHelper.BuildScopePredicate<TItem>(scope).And(predicate);

    private static Expression<Func<TItem, bool>> CombineSecondScope<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>>? predicate)
        where TItem : Entity, ISecondScopedById
    {
        var scoped = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return predicate is null ? scoped : scoped.And(predicate);
    }

    private Task<IAsyncEnumerable<TItem>> ScopedAllAsync<TItem>(string scope, bool includeDeleted, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
        => ScopedManyAsync<TItem>(scope, null, null, null, null, null, includeDeleted, transaction, cancellationToken);

    private Task<IAsyncEnumerable<TItem>> ScopedManyAsync<TItem>(string scope, Expression<Func<TItem, bool>>? predicate, string? continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>>? sortOrders, bool includeDeleted, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
        => Many<TItem>(CombineScope(scope, predicate), continueFrom!, pageSize, pageNumber, sortOrders!, includeDeleted, transaction, cancellationToken);

    private Task<TItem> ScopedByIdAsync<TItem>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var normalized = NormalizeId(id);

        return normalized is null
            ? Task.FromResult<TItem>(null!)
            : ScopedOneAsync<TItem>(scope, item => item.Id == normalized, null, null, includeDeleted, transaction, cancellationToken);
    }

    private Task<IAsyncEnumerable<TItem>> ScopedByIdsAsync<TItem>(string scope, IEnumerable<string> ids, bool includeDeleted,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var normalized = NormalizeEntityIds(ids);
        return ScopedManyAsync<TItem>(scope, item => normalized.Contains(item.Id), null, normalized.Count, null, null, includeDeleted, transaction, cancellationToken);
    }

    private Task<long> ScopedCountAsync<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string? continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, IScopedById
        => Count<TItem>(CombineScope(scope, predicate), continueFrom!, includeDeleted, transaction, cancellationToken);

    private IQueryable<TItem> ScopedQueryable<TItem>(string scope, bool includeDeleted) where TItem : Entity, IScopedById
        => IQueryable<TItem>(includeDeleted).Where(CombineScope<TItem>(scope, null));

    private Task<TItem> ScopedOneAsync<TItem>(string scope, Expression<Func<TItem, bool>>? predicate, string? continueFrom,
        IEnumerable<SortOrder<TItem>>? sortOrders, bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
        => One<TItem>(CombineScope(scope, predicate), continueFrom!, sortOrders!, includeDeleted, transaction, cancellationToken);

    private Task<IAsyncEnumerable<TItem>> ScopedRandomAsync<TItem>(string scope, Expression<Func<TItem, bool>>? predicate, string? continueFrom,
        int count, bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, IScopedById
        => Random<TItem>(CombineScope(scope, predicate), continueFrom!, count, includeDeleted, transaction, cancellationToken);

    private Task ScopedDeleteAsync<TItem>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
        => Delete<TItem>(CombineScope(scope, filter), transaction, cancellationToken);

    private async Task ScopedDeleteByIdAsync<TItem>(string scope, string id, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return;
        }

        await ScopedDeleteAsync<TItem>(scope, item => item.Id == normalized, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedDeleteByIdsAsync<TItem>(string scope, IEnumerable<string> ids, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var normalized = NormalizeEntityIds(ids);

        if (normalized.Count == 0)
        {
            return;
        }

        await ScopedDeleteAsync<TItem>(scope, item => normalized.Contains(item.Id), transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedInsertAsync<TItem>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Insert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedInsertManyAsync<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Insert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedSaveAsync<TItem>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Save<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedSaveManyAsync<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Save<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedUpdateAsync<TItem>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Update<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedUpdateWhereAsync<TItem>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Update<TItem>(CombineScope(scope, conditionPredicate), entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedUpdateManyAsync<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Update<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedUpsertAsync<TItem>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, IScopedById
    {
        ScopeModelHelper.SetScope(entity, scope);
        await Upsert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScopedUpsertManyAsync<TItem>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, IScopedById
    {
        var list = entities.ToList();
        list.ForEach(entity => ScopeModelHelper.SetScope(entity, scope));
        await Upsert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private Task ScopedJsonUpdateAsync<TItem>(string id, int version, string json, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity
        => JsonUpdate<TItem>(id, version, json, transaction, cancellationToken);

    private Task<IAsyncEnumerable<TItem>> SecondScopedAllAsync<TItem>(string primaryScope, string secondScope, bool includeDeleted,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
        => SecondScopedManyAsync<TItem>(primaryScope, secondScope, null, null, null, null, null, includeDeleted, transaction, cancellationToken);

    private Task<IAsyncEnumerable<TItem>> SecondScopedManyAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>>? predicate,
        string? continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>>? sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
        => Many<TItem>(CombineSecondScope(primaryScope, secondScope, predicate), continueFrom!, pageSize, pageNumber, sortOrders!, includeDeleted, transaction, cancellationToken);

    private Task<TItem> SecondScopedByIdAsync<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var normalized = NormalizeId(id);

        return normalized is null
            ? Task.FromResult<TItem>(null!)
            : SecondScopedOneAsync<TItem>(primaryScope, secondScope, item => item.Id == normalized, null, null, includeDeleted, transaction, cancellationToken);
    }

    private Task<IAsyncEnumerable<TItem>> SecondScopedByIdsAsync<TItem>(string primaryScope, string secondScope, IEnumerable<string> ids,
        bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var normalized = NormalizeEntityIds(ids);
        return SecondScopedManyAsync<TItem>(primaryScope, secondScope, item => normalized.Contains(item.Id), null, normalized.Count, null, null, includeDeleted, transaction, cancellationToken);
    }

    private Task<long> SecondScopedCountAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string? continueFrom,
        bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
        => Count<TItem>(CombineSecondScope(primaryScope, secondScope, predicate), continueFrom!, includeDeleted, transaction, cancellationToken);

    private IQueryable<TItem> SecondScopedQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted)
        where TItem : Entity, ISecondScopedById
        => IQueryable<TItem>(includeDeleted).Where(CombineSecondScope<TItem>(primaryScope, secondScope, null));

    private Task<TItem> SecondScopedOneAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>>? predicate, string? continueFrom,
        IEnumerable<SortOrder<TItem>>? sortOrders, bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, ISecondScopedById
        => One<TItem>(CombineSecondScope(primaryScope, secondScope, predicate), continueFrom!, sortOrders!, includeDeleted, transaction, cancellationToken);

    private Task<IAsyncEnumerable<TItem>> SecondScopedRandomAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>>? predicate,
        string? continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction, CancellationToken cancellationToken)
        where TItem : Entity, ISecondScopedById
        => Random<TItem>(CombineSecondScope(primaryScope, secondScope, predicate), continueFrom!, count, includeDeleted, transaction, cancellationToken);

    private Task SecondScopedDeleteAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> filter,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
        => Delete<TItem>(CombineSecondScope(primaryScope, secondScope, filter), transaction, cancellationToken);

    private async Task SecondScopedDeleteByIdAsync<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return;
        }

        await SecondScopedDeleteAsync<TItem>(primaryScope, secondScope, item => item.Id == normalized, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedDeleteByIdsAsync<TItem>(string primaryScope, string secondScope, IEnumerable<string> ids,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var normalized = NormalizeEntityIds(ids);

        if (normalized.Count == 0)
        {
            return;
        }

        await SecondScopedDeleteAsync<TItem>(primaryScope, secondScope, item => normalized.Contains(item.Id), transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedInsertAsync<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Insert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedInsertManyAsync<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entities,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var list = entities.ToList();

        foreach (var entity in list)
        {
            ScopeModelHelper.SetScope(entity, primaryScope);
            ScopeModelHelper.SetSecondScope(entity, secondScope);
        }

        await Insert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedSaveAsync<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Save<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedSaveManyAsync<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entities,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var list = entities.ToList();

        foreach (var entity in list)
        {
            ScopeModelHelper.SetScope(entity, primaryScope);
            ScopeModelHelper.SetSecondScope(entity, secondScope);
        }

        await Save<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedUpdateAsync<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Update<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedUpdateWhereAsync<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> conditionPredicate,
        TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Update<TItem>(CombineSecondScope(primaryScope, secondScope, conditionPredicate), entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedUpdateManyAsync<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entities,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var list = entities.ToList();

        foreach (var entity in list)
        {
            ScopeModelHelper.SetScope(entity, primaryScope);
            ScopeModelHelper.SetSecondScope(entity, secondScope);
        }

        await Update<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedUpsertAsync<TItem>(string primaryScope, string secondScope, TItem entity, IDatabaseTransaction transaction,
        CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        ScopeModelHelper.SetScope(entity, primaryScope);
        ScopeModelHelper.SetSecondScope(entity, secondScope);
        await Upsert<TItem>(entity, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task SecondScopedUpsertManyAsync<TItem>(string primaryScope, string secondScope, IEnumerable<TItem> entities,
        IDatabaseTransaction transaction, CancellationToken cancellationToken) where TItem : Entity, ISecondScopedById
    {
        var list = entities.ToList();

        foreach (var entity in list)
        {
            ScopeModelHelper.SetScope(entity, primaryScope);
            ScopeModelHelper.SetSecondScope(entity, secondScope);
        }

        await Upsert<TItem>(list, transaction, cancellationToken).ConfigureAwait(false);
    }
}
