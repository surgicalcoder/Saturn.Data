using System.Globalization;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IReadonlyRepository
{
    public Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => All<TItem>(includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var predicate = WithSoftDeleteFilter<TItem>(null, includeDeleted);
        var list = predicate is null
            ? (await store.Query<TItem>().ToList().ConfigureAwait(false)).ToList()
            : (await store.Query<TItem>().Where(predicate).ToList().ConfigureAwait(false)).ToList();

        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(id, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        return await GetFilteredByIdAsync<TItem>(id, includeDeleted, cancellationToken).ConfigureAwait(false);
    }

    public Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var normalized = NormalizeEntityIds(IDs);

        if (normalized.Count == 0)
        {
            return AsyncEnumerableFactory.From(Array.Empty<TItem>(), cancellationToken);
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var predicate = WithSoftDeleteFilter<TItem>(item => normalized.Contains(item.Id), includeDeleted);
        var list = (await store.Query<TItem>().Where(predicate).ToList().ConfigureAwait(false)).ToList();

        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Count(predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var effective = WithSoftDeleteFilter(predicate, includeDeleted);

        return effective is null
            ? await store.Query<TItem>().Count().ConfigureAwait(false)
            : await store.Query<TItem>().Where(effective).Count().ConfigureAwait(false);
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
        => IQueryable<TItem>(includeDeleted: false);

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        InitializeAsync().GetAwaiter().GetResult();

        var predicate = WithSoftDeleteFilter<TItem>(null, includeDeleted);
        var list = predicate is null
            ? store.Query<TItem>().ToList().GetAwaiter().GetResult().ToList()
            : store.Query<TItem>().Where(predicate).ToList().GetAwaiter().GetResult().ToList();

        return list.AsQueryable();
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var effective = WithSoftDeleteFilter(predicate, includeDeleted);

        var query = effective is null
            ? store.Query<TItem>()
            : store.Query<TItem>().Where(effective);

        if (pageSize.HasValue)
        {
            var skip = pageNumber.HasValue && pageNumber.Value > 1
                ? (pageNumber.Value - 1) * pageSize.Value
                : 0;

            query = query.Paginate(skip, pageSize.Value);
        }

        var list = (await query.ToList().ConfigureAwait(false)).ToList();

        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize, int? pageNumber,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Many(BuildWhereClausePredicate<TItem>(whereClause), continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);

    public Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => One(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, IEnumerable<SortOrder<TItem>> sortOrders,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var effective = WithSoftDeleteFilter(predicate, includeDeleted);
        var list = (await store.Query<TItem>().Where(effective).ToList().ConfigureAwait(false)).ToList();

        return list.Count == 0 ? null : list[0];
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Random is implemented in Phase 2.");

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => throw new NotSupportedException("Random is implemented in Phase 2.");

    private async Task<TItem> GetFilteredByIdAsync<TItem>(string id, bool includeDeleted, CancellationToken cancellationToken) where TItem : Entity
    {
        var normalized = NormalizeId(id);

        if (normalized is null)
        {
            return null;
        }

        var entity = await store.Get<TItem>(normalized).ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        if (!includeDeleted && SupportsSoftDelete<TItem>() && entity is ISoftDeletable { IsDeleted: true })
        {
            return null;
        }

        return entity;
    }

    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(Dictionary<string, object> whereClause) where TItem : Entity
    {
        if (whereClause is null || whereClause.Count == 0)
        {
            return item => true;
        }

        var parameter = Expression.Parameter(typeof(TItem), "item");
        Expression body = null;

        foreach (var pair in whereClause)
        {
            if (pair.Value is null)
            {
                continue;
            }

            var property = parameter.Type.GetProperty(pair.Key);

            if (property is null)
            {
                throw new NotSupportedException($"Unknown property '{pair.Key}' on '{parameter.Type.Name}'.");
            }

            var member = Expression.Property(parameter, property);
            Expression constant = property.PropertyType == typeof(string)
                ? Expression.Constant(Convert.ToString(pair.Value, CultureInfo.InvariantCulture))
                : Expression.Convert(Expression.Constant(pair.Value), property.PropertyType);

            var comparison = Expression.Equal(member, constant);
            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        return body is null ? item => true : Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }
}
