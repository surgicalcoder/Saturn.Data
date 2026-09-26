using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite;

public partial class SqliteRepository : IScopedReadonlyRepository
{
    public Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedAllAsync<TItem>(scope, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedAllAsync<TItem>(scope, includeDeleted, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedByIdAsync<TItem>(scope, id, false, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TScope>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedByIdAsync<TItem>(scope, id, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedByIdsAsync<TItem>(scope, IDs, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedByIdsAsync<TItem>(scope, IDs, includeDeleted, transaction, cancellationToken);

    public Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedCountAsync<TItem>(scope, predicate, continueFrom, false, transaction, cancellationToken);

    public Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedCountAsync<TItem>(scope, predicate, continueFrom, includeDeleted, transaction, cancellationToken);

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedQueryable<TItem>(scope, false);

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope, bool includeDeleted)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedQueryable<TItem>(scope, includeDeleted);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedManyAsync<TItem>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedManyAsync<TItem>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null!,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedManyAsync<TItem>(scope, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedManyAsync<TItem>(scope, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedOneAsync<TItem>(scope, predicate, continueFrom, sortOrders, false, transaction, cancellationToken);

    public Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedOneAsync<TItem>(scope, predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null!,
        string continueFrom = null!, int count = 1, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedRandomAsync<TItem>(scope, predicate, continueFrom, count, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int count, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
        => ScopedRandomAsync<TItem>(scope, predicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
}
