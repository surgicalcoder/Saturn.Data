using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class Repository : IReadonlyRepository
{
    public async Task<T> ById<T>(string id) where T : Entity
    {
        return await (await GetCollection<T>().FindAsync(e => e.Id == id, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();
    }

    public async Task<List<T>> ById<T>(List<string> IDs) where T : Entity
    {
        var result = await GetCollection<T>().FindAsync(e => IDs.Contains(e.Id));
        return await result.ToListAsync().ConfigureAwait(false);
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
        var findOptions = new FindOptions<T> { Limit = 1 };
        
        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<T> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }
        
        var result = await GetCollection<T>().FindAsync(predicate, findOptions);

        return await result.FirstOrDefaultAsync().ConfigureAwait(false);
    }

    private static SortDefinition<T> getSortDefinition<T>(IEnumerable<SortOrder<T>> sortOrders, SortDefinition<T> sortDefinition) where T : Entity
    {
        foreach (var sortOrder in sortOrders)
        {
            if (sortOrder.Direction == SortDirection.Ascending)
            {
                sortDefinition = sortDefinition == null ? Builders<T>.Sort.Ascending(entity => sortOrder.Field.Invoke(entity)) : sortDefinition.Ascending(entity => sortOrder.Field.Invoke(entity));
            }
            else
            {
                sortDefinition = sortDefinition == null ? Builders<T>.Sort.Descending(entity => sortOrder.Field.Invoke(entity)) : sortDefinition.Descending(entity => sortOrder.Field.Invoke(entity));
            }
        }

        return sortDefinition;
    }

    public async Task<T> Random<T>() where T : Entity
    {
        var item = await GetCollection<T>().AsQueryable().Sample(1).FirstOrDefaultAsync();
        return item;
    }

    public async Task<List<T>> Random<T>(int count) where T : Entity
    {
        var item = await GetCollection<T>().AsQueryable().Sample(count).ToListAsync();
        return item;
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        return await Task.Run(() => Queryable.Where(GetCollection<T>().AsQueryable(), predicate));
    }

    public async Task<List<T>> Many<T>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        var where = new BsonDocument(whereClause);
        var result = await (await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>()).FindAsync(where, null)).ToListAsync();
        
        return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<T>(predicate).ConfigureAwait(false);
        }

        return await Task.Run(() => Queryable.Take(GetCollection<T>().AsQueryable().Where(predicate).Skip((pageNumber - 1) * pageSize), pageSize)).ConfigureAwait(false);
    }

    public async Task<List<T>> Many<T>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<T>> sortOrders = null) where T : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<T>(whereClause).ConfigureAwait(false);
        }

        var where = new BsonDocument(whereClause);
        var result = await(await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>()).FindAsync(where, new FindOptions<BsonDocument>()
        {
            Skip = (pageNumber - 1) * pageSize,
            Limit = pageSize,
        } )).ToListAsync();

        return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
    }

    public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate) where T : Entity
    {
        return await GetCollection<T>().CountDocumentsAsync(predicate);
    }

    public async Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation operationFilter, Action<T, string, ChangeOperation> callback) where T : Entity
    {
        var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<T>>();

        var expression = Converter<ChangeStreamDocument<T>>.Convert(predicate);

        var opType = (ChangeStreamOperationType) operationFilter;

        var definition = pipelineDefinition.Match(expression).Match(e=>e.OperationType == opType);

        await GetCollection<T>().WatchAsync(definition);

        var collection = GetCollection<T>();

        using (var asyncCursor = await collection.WatchAsync(pipelineDefinition))
        {
            foreach (var changeStreamDocument in asyncCursor.ToEnumerable())
            {
                callback.Invoke(changeStreamDocument.FullDocument, changeStreamDocument?.DocumentKey[0]?.AsObjectId.ToString(), (ChangeOperation) changeStreamDocument.OperationType );
            }
        }
    }
}