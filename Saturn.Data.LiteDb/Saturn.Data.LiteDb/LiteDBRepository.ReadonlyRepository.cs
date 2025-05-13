using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await GetCollection<TItem>().FindByIdAsync(id);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var results = await GetCollection<TItem>().FindAsync(entity => IDs.Contains(entity.Id));

        return results.ToAsyncEnumerable();
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem>(List<Ref<TItem>> items, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, new()
    {
        var ids = items.Where(e => !string.IsNullOrWhiteSpace(e.Id)).Select(e => e.Id).ToList();
        var entities = await ById<TItem>(ids, transaction, cancellationToken);

        return await AsyncEnumerable.ToListAsync(entities.Select(e => new Ref<TItem>(e)), cancellationToken);
    }

    public async Task<TItem> ByRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, new()
    {
        return (string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem>(item.Id, cancellationToken: cancellationToken))!;
    }

    public async Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        item.Item = await ById<TItem>(item.Id);

        return item;
    }

    public Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Task.FromResult(GetCollection<TItem>().AsQueryable().ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) =>
                sortOrder.Direction == SortDirection.Ascending
                    ? current.OrderBy(sortOrder.Field)
                    : current.OrderByDescending(sortOrder.Field));
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TItem> Random<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var item = await GetCollection<TItem>().AsQueryable().OrderBy(e => Guid.NewGuid()).Take(1).FirstOrDefaultAsync(cancellationToken);

        return item;
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var item = GetCollection<TItem>().AsQueryable().OrderBy(e => Guid.NewGuid()).Take(count);

        return Task.FromResult(item.ToAsyncEnumerable());
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity
    {
        var items = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return items.ToAsyncEnumerable();
    }


    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "entity");
            var property = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, parameter);
            query = query.Where(lambda);
        }

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return Task.FromResult(query.ToAsyncEnumerable());
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many(predicate, cancellationToken: cancellationToken);
        }

        var items = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToAsyncEnumerable();
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "entity");
            var property = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, parameter);
            query = query.Where(lambda);
        }

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        if (pageSize > 0 && pageNumber > 0)
        {
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        return Task.FromResult(query.ToAsyncEnumerable());
    }

    public async Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await GetCollection<TItem>().LongCountAsync(predicate);
    }

    public Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        throw new NotImplementedException();
    }
}