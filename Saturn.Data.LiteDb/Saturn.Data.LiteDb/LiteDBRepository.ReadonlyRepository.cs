using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IReadonlyRepository
{
    public async Task<T> ById<T>(string id) where T : Entity
    {
        return await GetCollection<T>().FindByIdAsync(id);
    }

    public async Task<IAsyncEnumerable<T>> ById<T>(List<string> IDs) where T : Entity
    {
        var results = await GetCollection<T>().FindAsync(entity => IDs.Contains(entity.Id));

        return results.ToAsyncEnumerable();
    }

    public async Task<List<Ref<T>>> ByRef<T>(List<Ref<T>> item) where T : Entity, new()
    {
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<T>(enumerable);

        return res.Select(r => new Ref<T>(r)).ToListAsync().Result;
    }

    public async Task<T> ByRef<T>(Ref<T> item) where T : Entity, new()
    {
        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<T>(item.Id);
    }

    public async Task<Ref<T>> PopulateRef<T>(Ref<T> item) where T : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        item.Item = await ById<T>(item.Id);

        return item;
    }

    public Task<IAsyncEnumerable<T>> All<T>() where T : Entity
    {
        return Task.FromResult(GetCollection<T>().AsQueryable().ToAsyncEnumerable());
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<T> One<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        var query = GetCollection<T>().AsQueryable().Where(predicate);
    
        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => 
                sortOrder.Direction == SortDirection.Ascending 
                    ? current.OrderBy(sortOrder.Field) 
                    : current.OrderByDescending(sortOrder.Field));
        }
    
        return await query.FirstOrDefaultAsync();
    }

    public async Task<T> Random<T>() where T : Entity
    {
        var item = await GetCollection<T>().AsQueryable().OrderBy(e => Guid.NewGuid()).Take(1).FirstOrDefaultAsync();

        return item;
    }

    public Task<IAsyncEnumerable<T>> Random<T>(int count) where T : Entity
    {
        var item = GetCollection<T>().AsQueryable().OrderBy(e => Guid.NewGuid()).Take(count);

        return Task.FromResult(item.ToAsyncEnumerable());
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        var items = GetCollection<T>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                items = sortOrder.Direction == SortDirection.Ascending ? items.OrderBy(sortOrder.Field) : items.OrderByDescending(sortOrder.Field);
            }
        }

        return await Task.Run(() => items);
    }

    public async Task<IAsyncEnumerable<T>> Many<T>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        var query = GetCollection<T>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equal, parameter);
            query = query.Where(lambda);
        }

        if (sortOrders != null)
        {
            query = sortOrders.Aggregate(query, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        var result = await Task.Run(() => query.ToAsyncEnumerable());

        return result;
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many(predicate).ConfigureAwait(false);
        }

        var items = GetCollection<T>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return await Task.Run(() => items.Skip((pageNumber - 1) * pageSize).Take(pageSize));
    }

    public async Task<IAsyncEnumerable<T>> Many<T>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        var query = GetCollection<T>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(equal, parameter);
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

        var result = await Task.Run(() => query.ToAsyncEnumerable());

        return result;
    }

    public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate) where T : Entity
    {
        return await GetCollection<T>().LongCountAsync(predicate);
    }

    public Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation operationFilter, Action<T, string, ChangeOperation> callback) where T : Entity
    {
        throw new NotImplementedException();
    }
}