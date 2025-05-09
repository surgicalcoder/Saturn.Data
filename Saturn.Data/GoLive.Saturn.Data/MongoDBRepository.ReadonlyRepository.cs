using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.AsyncEnumerable;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string id) where TItem : Entity
    {
        return await (await GetCollection<TItem>().FindAsync(e => e.Id == id, new FindOptions<TItem> { Limit = 1 })).FirstOrDefaultAsync();
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(List<string> IDs) where TItem : Entity
    {
        var result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id));

        return result.ToAsyncEnumerable();
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem>(List<Ref<TItem>> items) where TItem : Entity, new()
    {
        var ids = items.Where(e => !string.IsNullOrWhiteSpace(e.Id)).Select(e => e.Id).ToList();
        var entities = await ById<TItem>(ids);
        return await entities.Select(e => new Ref<TItem>(e)).ToListAsync();
    }

    public async Task<TItem> ByRef<TItem>(Ref<TItem> item) where TItem : Entity, new()
    {
        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem>(item.Id);
    }

    public async Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item) where TItem : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }
        item.Item = await ById<TItem>(item.Id);
        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>() where TItem : Entity
    {
        return (await GetCollection<TItem>().FindAsync(e => true)).ToAsyncEnumerable();
    }
    
    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        var findOptions = new FindOptions<TItem> { Limit = 1 };
        
        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<TItem> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }
        
        var result = await GetCollection<TItem>().FindAsync(predicate, findOptions);

        return await result.FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<TItem> Random<TItem>() where TItem : Entity
    {
        var item = await GetCollection<TItem>().AsQueryable().Sample(1).FirstOrDefaultAsync();
        return item;
    }

    public Task<IAsyncEnumerable<TItem>> Random<TItem>(int count) where TItem : Entity
    {
        var item = GetCollection<TItem>().AsQueryable().Sample(count);
        return Task.FromResult(item.ToAsyncEnumerable());
    }

    public async Task<IQueryable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        var items = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return await Task.Run(() => items);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        var where = new BsonDocument(whereClause);

        var findOptions = new FindOptions<TItem>();
        
        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }
        
        var res = await GetCollection<TItem>().FindAsync(where, findOptions);

        return res.ToAsyncEnumerable();
    }

    public async Task<IQueryable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem>(predicate).ConfigureAwait(false);
        }

        var items = GetCollection<TItem>().AsQueryable().Where(predicate);
        
        if (sortOrders != null)
        {
            foreach (var sortOrder in sortOrders)
            {
                items = sortOrder.Direction == SortDirection.Ascending ? items.OrderBy(sortOrder.Field) : items.OrderByDescending(sortOrder.Field);
            }
        }

        return await Task.Run(() => items.Skip((pageNumber - 1) * pageSize).Take(pageSize));
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem>(whereClause);
        }

        var where = new BsonDocument(whereClause);

        var findOptions = new FindOptions<TItem>
        {
            Skip = (pageNumber - 1) * pageSize,
            Limit = pageSize
        };
        
        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }
        
        var res = await GetCollection<TItem>().FindAsync(where, findOptions);

        return res.ToAsyncEnumerable();
    }

    public async Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
    {
        return await GetCollection<TItem>().CountDocumentsAsync(predicate);
    }

    public async Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : Entity
    {
        var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<TItem>>();

        var expression = Converter<ChangeStreamDocument<TItem>>.Convert(predicate);

        var opType = (ChangeStreamOperationType) operationFilter;

        var definition = pipelineDefinition.Match(expression).Match(e=>e.OperationType == opType);

        await GetCollection<TItem>().WatchAsync(definition);

        var collection = GetCollection<TItem>();

        using (var asyncCursor = await collection.WatchAsync(pipelineDefinition))
        {
            foreach (var changeStreamDocument in asyncCursor.ToEnumerable())
            {
                callback.Invoke(changeStreamDocument.FullDocument, changeStreamDocument?.DocumentKey[0]?.AsObjectId.ToString(), (ChangeOperation) changeStreamDocument.OperationType );
            }
        }
    }
}