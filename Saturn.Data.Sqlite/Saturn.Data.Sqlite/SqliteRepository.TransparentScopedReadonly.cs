using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : ITransparentScopedReadonlyRepository
{
    private string ResolveTransparentScope<TParent>() where TParent : Entity
    {
        var provider = options.TransparentScopeProvider;

        if (provider is null)
        {
            throw new InvalidOperationException("TransparentScopeProvider is not configured.");
        }

        return provider.Invoke(typeof(TParent));
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedAllAsync<TItem>(ResolveTransparentScope<TParent>(), false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedAllAsync<TItem>(ResolveTransparentScope<TParent>(), includeDeleted, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedByIdAsync<TItem>(ResolveTransparentScope<TParent>(), id, false, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TParent>(string id, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedByIdAsync<TItem>(ResolveTransparentScope<TParent>(), id, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedByIdsAsync<TItem>(ResolveTransparentScope<TParent>(), IDs, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedByIdsAsync<TItem>(ResolveTransparentScope<TParent>(), IDs, includeDeleted, transaction, cancellationToken);

    public Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedCountAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, false, transaction, cancellationToken);

    public Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedCountAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, includeDeleted, transaction, cancellationToken);

    public IQueryable<TItem> IQueryable<TItem, TParent>()
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedQueryable<TItem>(ResolveTransparentScope<TParent>(), false);

    public IQueryable<TItem> IQueryable<TItem, TParent>(bool includeDeleted)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedQueryable<TItem>(ResolveTransparentScope<TParent>(), includeDeleted);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedManyAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedManyAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null!,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedManyAsync<TItem>(ResolveTransparentScope<TParent>(), BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedManyAsync<TItem>(ResolveTransparentScope<TParent>(), BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedOneAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, sortOrders, false, transaction, cancellationToken);

    public Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedOneAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null!, string continueFrom = null!,
        int count = 1, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedRandomAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, count, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TParent>, new()
        where TParent : Entity, new()
        => ScopedRandomAsync<TItem>(ResolveTransparentScope<TParent>(), predicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
}
