using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IWeakSecondScopedReadonlyRepository
{
    public Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedAllAsync<TItem>(primaryScope, secondScope, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedAllAsync<TItem>(primaryScope, secondScope, includeDeleted, transaction, cancellationToken);

    public Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedByIdAsync<TItem>(primaryScope, secondScope, id, false, transaction, cancellationToken);

    public Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedByIdAsync<TItem>(primaryScope, secondScope, id, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedByIdsAsync<TItem>(primaryScope, secondScope, IDs, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedByIdsAsync<TItem>(primaryScope, secondScope, IDs, includeDeleted, transaction, cancellationToken);

    public Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedCountAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, false, transaction, cancellationToken);

    public Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedCountAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, includeDeleted, transaction, cancellationToken);

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedQueryable<TItem>(primaryScope, secondScope, false);

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedQueryable<TItem>(primaryScope, secondScope, includeDeleted);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom = null!, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedManyAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedManyAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause,
        string continueFrom = null!, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedManyAsync<TItem>(primaryScope, secondScope, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause,
        string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedManyAsync<TItem>(primaryScope, secondScope, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null!,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedOneAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, false, transaction, cancellationToken);

    public Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedOneAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null!,
        string continueFrom = null!, int count = 1, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedRandomAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, count, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
        => SecondScopedRandomAsync<TItem>(primaryScope, secondScope, predicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
}
