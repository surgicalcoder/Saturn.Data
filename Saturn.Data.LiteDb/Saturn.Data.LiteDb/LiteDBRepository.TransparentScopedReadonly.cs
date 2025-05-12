using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : ITransparentScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id);
    }

    async Task<List<TItem>> ITransparentScopedReadonlyRepository.ById<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await System.Linq.AsyncEnumerable.ToListAsync((await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken)), cancellationToken);
    }

    async Task<List<Ref<TItem>>> ITransparentScopedReadonlyRepository.ByRef<TItem, TParent>(List<Ref<TItem>> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<TItem, TParent>(scope, enumerable, cancellationToken: cancellationToken);
        return await System.Linq.AsyncEnumerable.ToListAsync(res.Select(r => new Ref<TItem>(r)), cancellationToken);
    }

    public async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem, TParent>(scope, item.Id);
    }

    public async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        item.Item = await ById<TItem, TParent>(scope, item.Id, cancellationToken: cancellationToken);

        return item;
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return All<TItem, TParent>(scope, cancellationToken: cancellationToken);
    }

    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await One<TItem, TParent>(scope, predicate, sortOrders, cancellationToken: cancellationToken);
    }

    public async Task<TItem> Random<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Take(1).FirstOrDefault();

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Take(count);

        return item.ToAsyncEnumerable();
    }

    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public IQueryable<TItem> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope).Where(predicate);

        if (sortOrders != null)
        {
            scopedEntities = sortOrders.Aggregate(scopedEntities, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return scopedEntities;
    }
    
    public IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return Many<TItem, TParent>(scope, predicate, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
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

    public IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return Many<TItem, TParent>(scope, predicate, pageSize, pageNumber, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
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

    public async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await CountMany<TItem, TParent>(scope, predicate, cancellationToken: cancellationToken);
    }

    public Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
}