using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : ISecondScopedReadonlyRepository
{
    public Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedAllAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedAllAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, includeDeleted, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedByIdAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, id, false, transaction, cancellationToken);

    public Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedByIdAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, id, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IEnumerable<string> IDs, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedByIdsAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, IDs, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedByIdsAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, IDs, includeDeleted, transaction, cancellationToken);

    public Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null!, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedCountAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, false, transaction, cancellationToken);

    public Task<long> Count<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedCountAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, includeDeleted, transaction, cancellationToken);

    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedQueryable<TItem>(primaryScope?.Id!, secondScope?.Id!, false);

    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        bool includeDeleted)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedQueryable<TItem>(primaryScope?.Id!, secondScope?.Id!, includeDeleted);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null!, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedManyAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedManyAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Dictionary<string, object> whereClause, string continueFrom = null!, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null!, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedManyAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedManyAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom = null!, IEnumerable<SortOrder<TItem>> sortOrders = null!,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedOneAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, sortOrders, false, transaction, cancellationToken);

    public Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null!, CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedOneAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate = null!, string continueFrom = null!, int count = 1, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedRandomAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, count, false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope,
        Expression<Func<TItem, bool>> predicate, string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null!,
        CancellationToken cancellationToken = default)
        where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new()
        where TSecondScope : Entity, new()
        where TPrimaryScope : Entity, new()
        => SecondScopedRandomAsync<TItem>(primaryScope?.Id!, secondScope?.Id!, predicate, continueFrom, count, includeDeleted, transaction, cancellationToken);
}
