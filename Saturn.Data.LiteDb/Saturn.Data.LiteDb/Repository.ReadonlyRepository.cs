using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.LiteDb;

public partial class Repository : IReadonlyRepository
{
    public async Task<T> ById<T>(string id) where T : Entity
    {
        return await GetCollection<T>().FindByIdAsync(id);
    }

    public async Task<List<T>> ById<T>(List<string> IDs) where T : Entity
    {
        return (await GetCollection<T>().FindAsync(entity => IDs.Contains(entity.Id))).ToList();
    }

    public async Task<List<Ref<T>>> ByRef<T>(List<Ref<T>> item) where T : Entity, new()
    {
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<T>(enumerable);
        return res.Select(r => new Ref<T>(r)).ToList();
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

    public async Task<IQueryable<T>> All<T>() where T : Entity
    {
        return await Task.Run(() => GetCollection<T>().AsQueryable()).ConfigureAwait(false);
    }

    public async Task<T> One<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        return await GetCollection<T>().FindOneAsync(predicate); // TODO need to implement Sort Orders
    }

    public async Task<T> Random<T>() where T : Entity
    {
        var item = await GetCollection<T>().AsQueryable().OrderBy(e=>Guid.NewGuid()).Take(1).FirstOrDefaultAsync();
        return item;
    }

    public async Task<List<T>> Random<T>(int count) where T : Entity
    {
        var item = await GetCollection<T>().AsQueryable().OrderBy(e=>Guid.NewGuid()).Take(count).ToListAsync();
        return item;
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

    public async Task<List<T>> Many<T>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity // TODO
    {
        throw new NotImplementedException();
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity 
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<T>(predicate).ConfigureAwait(false);
        }

        var items = GetCollection<T>().AsQueryable().Where(predicate);
        
        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                items = sortOrder.Direction == SortDirection.Ascending ? items.OrderBy(sortOrder.Field) : items.OrderByDescending(sortOrder.Field);
            }
        }

        return await Task.Run(() => items.Skip((pageNumber - 1) * pageSize).Take(pageSize));
    }

    public async Task<List<T>> Many<T>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity // TODO
    {
        throw new NotImplementedException();
    }

    public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate) where T : Entity
    {
        return await GetCollection<T>().LongCountAsync(predicate);
    }

    public async Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation operationFilter, Action<T, string, ChangeOperation> callback) where T : Entity
    {
        throw new NotImplementedException();
    }
}