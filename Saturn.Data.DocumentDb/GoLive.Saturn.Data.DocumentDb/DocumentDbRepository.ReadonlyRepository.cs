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
        var list = await QueryRunner.ExecuteAsync(predicate, null, false, null, null, cancellationToken).ConfigureAwait(false);

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
        var list = await QueryRunner.ExecuteAsync(predicate, null, false, null, null, cancellationToken).ConfigureAwait(false);

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
        var token = NormalizeId(continueFrom);

        if (token is null)
        {
            return await QueryRunner.CountAsync(effective, cancellationToken).ConfigureAwait(false);
        }

        var candidates = await QueryRunner.ExecuteAsync(effective, null, false, null, null, cancellationToken).ConfigureAwait(false);

        return candidates.Count(item => string.CompareOrdinal(item.Id, token) > 0);
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
        => IQueryable<TItem>(includeDeleted: false);

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        InitializeAsync().GetAwaiter().GetResult();

        var predicate = WithSoftDeleteFilter<TItem>(null, includeDeleted);
        var list = QueryRunner.ExecuteAsync(predicate, null, false, null, null, CancellationToken.None).GetAwaiter().GetResult();

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
        var orderBy = BuildOrderBySelector(sortOrders, out var descending);
        var token = CanApplyContinuation(sortOrders) ? NormalizeId(continueFrom) : null;

        int? skip = null;
        int? take = pageSize;

        if (token is null && pageSize.HasValue && pageNumber.HasValue && pageNumber.Value > 1)
        {
            skip = (pageNumber.Value - 1) * pageSize.Value;
        }

        var list = await QueryRunner.ExecuteAsync(effective, orderBy, descending, token is null ? skip : null, token is null ? take : null, cancellationToken).ConfigureAwait(false);

        if (token is not null)
        {
            IEnumerable<TItem> filtered = list.Where(item => string.CompareOrdinal(item.Id, token) > 0);

            if (take.HasValue)
            {
                filtered = filtered.Take(take.Value);
            }

            list = filtered.ToList();
        }

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
        var orderBy = BuildOrderBySelector(sortOrders, out var descending);
        var token = CanApplyContinuation(sortOrders) ? NormalizeId(continueFrom) : null;

        if (token is null)
        {
            return await QueryRunner.FirstOrDefaultAsync(effective, orderBy, descending, cancellationToken).ConfigureAwait(false);
        }

        var candidates = await QueryRunner.ExecuteAsync(effective, orderBy, descending, null, null, cancellationToken).ConfigureAwait(false);
        var list = candidates.Where(item => string.CompareOrdinal(item.Id, token) > 0).ToList();

        return list.Count == 0 ? null : list[0];
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
        => Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var effective = WithSoftDeleteFilter(predicate, includeDeleted);
        var total = await QueryRunner.CountAsync(effective, cancellationToken).ConfigureAwait(false);

        if (total <= 0 || count <= 0)
        {
            return AsyncEnumerableFactory.From(Array.Empty<TItem>(), cancellationToken);
        }

        var wanted = (int)Math.Min(count, total);
        var maxSkip = (int)Math.Min(total - wanted, int.MaxValue);
        var skip = System.Random.Shared.Next(0, maxSkip + 1);
        var list = await QueryRunner.ExecuteAsync(effective, null, false, skip, wanted, cancellationToken).ConfigureAwait(false);

        return AsyncEnumerableFactory.From(list, cancellationToken);
    }

    private static Expression<Func<TItem, object>> BuildOrderBySelector<TItem>(IEnumerable<SortOrder<TItem>> sortOrders, out bool descending) where TItem : Entity
    {
        descending = false;

        var first = sortOrders?.FirstOrDefault();

        if (first?.Field is null)
        {
            return null;
        }

        descending = first.Direction == SortDirection.Descending;
        return first.Field;
    }

    private static bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        var first = sortOrders?.FirstOrDefault();

        if (first?.Field is null)
        {
            return true;
        }

        if (!Query.MemberPathResolver.TryResolve(first.Field, out var path))
        {
            return false;
        }

        return string.Equals(path, nameof(Entity.Id), StringComparison.Ordinal)
               && first.Direction == SortDirection.Ascending;
    }

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
