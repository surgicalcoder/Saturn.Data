using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository //: ITransparentScopedReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id);
    }

    public virtual async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await AsyncEnumerable.ToListAsync(await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken), cancellationToken);
    }

    public virtual async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<TItem, TParent>(scope, enumerable, cancellationToken: cancellationToken);

        return await AsyncEnumerable.ToListAsync(res.Select(r => new Ref<TItem>(r)), cancellationToken);
    }

    public virtual async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem, TParent>(scope, item.Id);
    }

    public virtual async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        item.Item = await ById<TItem, TParent>(scope, item.Id, cancellationToken: cancellationToken);

        return item;
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return All<TItem, TParent>(scope, cancellationToken: cancellationToken);
    }

    public virtual async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await One<TItem, TParent>(scope, TransformRefEntityComparisons(predicate), sortOrders, cancellationToken: cancellationToken);
    }

    public virtual async Task<TItem> Random<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Take(1).FirstOrDefault();

        return item;
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Take(count);

        return item.ToAsyncEnumerable();
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        var query = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        query = query.Where(TransformRefEntityComparisons(predicate));

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) =>
                sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return query.ToAsyncEnumerable();
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await Many<TItem, TParent>(scope, TransformRefEntityComparisons(predicate), pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var query = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "x");
            var member = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var body = Expression.Equal(member, constant);
            var lambda = Expression.Lambda<Func<TItem, bool>>(body, parameter);
            query = query.Where(lambda);
        }

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return query.ToAsyncEnumerable();
    }

    public virtual async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var query = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "x");
            var member = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var body = Expression.Equal(member, constant);
            var lambda = Expression.Lambda<Func<TItem, bool>>(body, parameter);
            query = query.Where(lambda);
        }

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        if (pageSize != 0 && pageNumber != 0)
        {
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        return query.ToAsyncEnumerable();
    }

    public virtual async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await CountMany<TItem, TParent>(scope, TransformRefEntityComparisons(predicate), cancellationToken: cancellationToken);
    }

    public virtual Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    
}