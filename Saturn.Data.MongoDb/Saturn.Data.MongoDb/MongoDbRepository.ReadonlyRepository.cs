using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, e => true, cancellationToken: cancellationToken),
            collection => collection.FindAsync(e => true, cancellationToken: cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }
    
    public async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var findOptions = BuildFindOptions<TItem>(limit: 1);
        
        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, e => e.Id == id, findOptions, cancellationToken),
            collection => collection.FindAsync(e => e.Id == id, findOptions, cancellationToken)
        );

        return await result.FirstOrDefaultAsync(cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, e => IDs.Contains(e.Id), cancellationToken: cancellationToken),
            collection => collection.FindAsync(e => IDs.Contains(e.Id), cancellationToken: cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }
    
    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, predicate, cancellationToken: cancellationToken),
            collection => collection.CountDocumentsAsync(predicate, cancellationToken: cancellationToken)
        );
    }
    
    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var filter = BuildFilterWithContinuation(predicate, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }


    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        FilterDefinition<TItem> baseFilter = new BsonDocument(whereClause);
        var filter = BuildFilterWithContinuation(baseFilter, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );
    
        return result.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var filter = BuildFilterWithContinuation(predicate, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, limit: 1);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );

        return await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var aggregate = transaction != null
            ? GetCollection<TItem>().Aggregate(((MongoDbTransactionWrapper)transaction).Session)
            : GetCollection<TItem>().Aggregate();

        var result = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));

        return result.ToAsyncEnumerable();
    }
}