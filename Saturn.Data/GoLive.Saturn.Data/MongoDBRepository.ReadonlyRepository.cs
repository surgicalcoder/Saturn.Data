using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
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
    async Task<TItem> IReadonlyRepository.ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        if (transaction != null)
        {
            return await (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == id, new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
        else
        {
            return await (await GetCollection<TItem>().FindAsync(e => e.Id == id, new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
    }

    async Task<IAsyncEnumerable<TItem>> IReadonlyRepository.ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => IDs.Contains(e.Id), cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id), cancellationToken: cancellationToken);
        }

        return result.ToAsyncEnumerable();
    }


    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => IDs.Contains(e.Id), cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id), cancellationToken: cancellationToken);
        }

        return result.ToAsyncEnumerable();
    }

    async Task<List<Ref<TItem>>> IReadonlyRepository.ByRef<TItem>(List<Ref<TItem>> items, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var ids = items.Where(e => !string.IsNullOrWhiteSpace(e.Id)).Select(e => e.Id).ToList();
        var entities = await ById<TItem>(ids, transaction, cancellationToken: cancellationToken);

        return await System.Linq.AsyncEnumerable.ToListAsync(entities.Select(e => new Ref<TItem>(e)), cancellationToken);
    }

    async Task<TItem> IReadonlyRepository.ByRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        if (transaction != null)
        {
            item.Item = await (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == item.Id, options: new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
        else
        {
            item.Item = await (await GetCollection<TItem>().FindAsync(e => e.Id == item.Id, options: new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }

        return item;
    }

    async Task<Ref<TItem>> IReadonlyRepository.PopulateRef<TItem>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }

        if (transaction != null)
        {
            item.Item = await (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == item.Id, options: new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
        else
        {
            item.Item = await (await GetCollection<TItem>().FindAsync(e => e.Id == item.Id, options: new FindOptions<TItem> { Limit = 1 }, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (transaction != null)
        {
            return (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => true, cancellationToken: cancellationToken)).ToAsyncEnumerable();
        }
        else
        {
            return (await GetCollection<TItem>().FindAsync(e => true, cancellationToken: cancellationToken)).ToAsyncEnumerable();
        }
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var findOptions = new FindOptions<TItem> { Limit = 1 };

        if (sortOrders != null && sortOrders.Any())
        {
            SortDefinition<TItem> sortDefinition = null;
            sortDefinition = getSortDefinition(sortOrders, sortDefinition);
            findOptions.Sort = sortDefinition;
        }

        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, predicate, findOptions, cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(predicate, findOptions, cancellationToken);
        }

        return await result.FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<TItem> Random<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        TItem item;

        if (transaction != null)
        {
            item = await GetCollection<TItem>().Aggregate(((MongoDBTransactionWrapper)transaction).Session).AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", 1))).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            item = await GetCollection<TItem>().Aggregate().AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", 1))).FirstOrDefaultAsync(cancellationToken);
        }

        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var aggregate = transaction != null
            ? GetCollection<TItem>().Aggregate(((MongoDBTransactionWrapper)transaction).Session)
            : GetCollection<TItem>().Aggregate();

        var result = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));

        return result.ToAsyncEnumerable();
    }

    public IQueryable<TItem> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        var items = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return items;
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var where = new BsonDocument(whereClause);

        var findOptions = new FindOptions<TItem>();

        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }

        IAsyncCursor<TItem> res;

        if (transaction != null)
        {
            res = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, where, findOptions, cancellationToken);
        }
        else
        {
            res = await GetCollection<TItem>().FindAsync(where, findOptions, cancellationToken);
        }

        return res.ToAsyncEnumerable();
    }

    public IQueryable<TItem> Many<TItem>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return Many<TItem>(predicate);
        }

        var items = GetCollection<TItem>().AsQueryable().Where(predicate);

        if (sortOrders != null)
        {
            items = sortOrders.Aggregate(items, (current, sortOrder) => sortOrder.Direction == SortDirection.Ascending ? current.OrderBy(sortOrder.Field) : current.OrderByDescending(sortOrder.Field));
        }

        return items.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem>(whereClause, sortOrders, transaction, cancellationToken);
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

        IAsyncCursor<TItem> res;

        if (transaction != null)
        {
            res = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, where, findOptions, cancellationToken);
        }
        else
        {
            res = await GetCollection<TItem>().FindAsync(where, findOptions, cancellationToken);
        }

        return res.ToAsyncEnumerable();
    }

    public async Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        if (transaction != null)
        {
            return await GetCollection<TItem>().CountDocumentsAsync(((MongoDBTransactionWrapper)transaction).Session, predicate, cancellationToken: cancellationToken);
        }
        else
        {
            return await GetCollection<TItem>().CountDocumentsAsync(predicate, cancellationToken: cancellationToken);
        }
    }

    public async Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<TItem>>();

        var expression = Converter<ChangeStreamDocument<TItem>>.Convert(predicate);

        var opType = (ChangeStreamOperationType)operationFilter;

        var definition = pipelineDefinition.Match(expression).Match(e => e.OperationType == opType);

        if (transaction != null)
        {
            await GetCollection<TItem>().WatchAsync(((MongoDBTransactionWrapper)transaction).Session, definition, cancellationToken: cancellationToken);
        }
        else
        {
            await GetCollection<TItem>().WatchAsync(definition, cancellationToken: cancellationToken);
        }

        var collection = GetCollection<TItem>();

        using (var asyncCursor = await collection.WatchAsync(pipelineDefinition, cancellationToken: cancellationToken))
        {
            foreach (var changeStreamDocument in asyncCursor.ToEnumerable())
            {
                callback.Invoke(changeStreamDocument.FullDocument, changeStreamDocument?.DocumentKey[0]?.AsObjectId.ToString(), (ChangeOperation)changeStreamDocument.OperationType);
            }
        }
    }
}