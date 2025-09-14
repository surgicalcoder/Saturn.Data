using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (transaction != null)
        {
            return (await GetCollection<TItem>().FindAsync(((MongoDbTransactionWrapper)transaction).Session, e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable();
        }

        return (await GetCollection<TItem>().FindAsync(e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable();
    }

    public async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        TItem result;

        if (transaction != null)
        {
            result = await (await GetCollection<TItem>().FindAsync(((MongoDbTransactionWrapper)transaction).Session, e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDbTransactionWrapper)transaction).Session, e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }


        return result.ToAsyncEnumerable();
    }

    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        long item;

        if (transaction != null)
        {
            item = await GetCollection<TItem>().CountDocumentsAsync(((MongoDbTransactionWrapper)transaction).Session, combinedPred, cancellationToken: cancellationToken);
        }
        else
        {
            item = await GetCollection<TItem>().CountDocumentsAsync(combinedPred, cancellationToken: cancellationToken);
        }

        return item;
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return scopedEntities;
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        predicate = predicate.And(item => item.Scope == scope);

        var filter = Builders<TItem>.Filter.Where(predicate);
        var findOptions = new FindOptions<TItem>();

        if (sortOrders != null && sortOrders.Any())
        {
            var sortDefinitions = sortOrders.Select(sortOrder => sortOrder.Direction == SortDirection.Ascending
                                                ? Builders<TItem>.Sort.Ascending(sortOrder.Field)
                                                : Builders<TItem>.Sort.Descending(sortOrder.Field))
                                            .ToList();
            findOptions.Sort = Builders<TItem>.Sort.Combine(sortDefinitions);
        }

        var res = await GetCollection<TItem>().FindAsync(filter, findOptions, cancellationToken);

        return res.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var scopedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = scope
        };

        var where = new BsonDocument(scopedWhereClause);

        var findOptions = new FindOptions<TItem>();

        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }

        IAsyncCursor<TItem> res;

        if (transaction != null)
        {
            res = await GetCollection<TItem>().FindAsync(((MongoDbTransactionWrapper)transaction).Session, where, findOptions, cancellationToken);
        }
        else
        {
            res = await GetCollection<TItem>().FindAsync(where, findOptions, cancellationToken);
        }

        return res.ToAsyncEnumerable();
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

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
            result = await GetCollection<TItem>().FindAsync(((MongoDbTransactionWrapper)transaction).Session, combinedPred, findOptions, cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(combinedPred, findOptions, cancellationToken);
        }

        return await result.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var combinedPredicate = predicate != null ? scopePredicate.And(predicate) : scopePredicate;

        var pipeline = new BsonDocument[]
        {
            new("$match", combinedPredicate.ToBsonDocument()),
            new("$sample", new BsonDocument("size", count))
        };

        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().AggregateAsync<TItem>(((MongoDbTransactionWrapper)transaction).Session, pipeline, cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().AggregateAsync<TItem>(pipeline, cancellationToken: cancellationToken);
        }

        return result.ToAsyncEnumerable();
    }
}