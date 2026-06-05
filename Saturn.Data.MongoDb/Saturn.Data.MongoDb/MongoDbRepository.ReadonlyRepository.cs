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
        return await All<TItem>(includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        Expression<Func<TItem, bool>> predicate = _ => true;
        var context = BuildReadContext(
            RepositoryReadOperation.All,
            includeDeleted: includeDeleted,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(Builders<TItem>.Filter.Where(predicate), includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, cancellationToken: cancellationToken),
            collection => collection.FindAsync(filter, cancellationToken: cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }
    
    public async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await ById<TItem>(id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string id, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        Expression<Func<TItem, bool>> predicate = item => item.Id == id;
        var context = BuildReadContext(
            RepositoryReadOperation.ById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var findOptions = BuildFindOptions<TItem>(limit: 1);
        var filter = ApplySoftDeleteFilter(
            Builders<TItem>.Filter.Where(predicate),
            includeDeleted);
        
        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );

        return await result.FirstOrDefaultAsync(cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var idList = IDs.ToList();
        Expression<Func<TItem, bool>> predicate = item => idList.Contains(item.Id);
        var context = BuildReadContext(
            RepositoryReadOperation.ByIds,
            includeDeleted: includeDeleted,
            ids: idList,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(
            Builders<TItem>.Filter.Where(predicate),
            includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, cancellationToken: cancellationToken),
            collection => collection.FindAsync(filter, cancellationToken: cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }
    
    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Count(predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var context = BuildReadContext(
            RepositoryReadOperation.Count,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), continueFrom);
        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken),
            collection => collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
        );
    }

    public async Task<bool> Exists<TItem>(string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        Expression<Func<TItem, bool>> predicate = item => item.Id == id;
        var context = BuildReadContext(
            RepositoryReadOperation.ExistsById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(Builders<TItem>.Filter.Where(predicate), includeDeleted);
        var countOptions = new CountOptions { Limit = 1 };

        var count = await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, countOptions, cancellationToken),
            collection => collection.CountDocumentsAsync(filter, countOptions, cancellationToken)
        );

        return count > 0;
    }

    public async Task<bool> Exists<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var context = BuildReadContext(
            RepositoryReadOperation.ExistsByPredicate,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), continueFrom);
        var countOptions = new CountOptions { Limit = 1 };

        var count = await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, countOptions, cancellationToken),
            collection => collection.CountDocumentsAsync(filter, countOptions, cancellationToken)
        );

        return count > 0;
    }
    
    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return IQueryable<TItem>(includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(bool includeDeleted) where TItem : Entity
    {
        var context = BuildReadContext<TItem>(RepositoryReadOperation.Queryable, includeDeleted: includeDeleted);
        var query = ApplyReadBehaviors(GetCollection<TItem>().AsQueryable(), context);
        return ApplySoftDeleteFilter(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.Many,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: pageSize,
            pageNumber: pageNumber,
            sortOrders: sortOrdersList,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, pageSize, pageNumber, effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );

        return result.ToAsyncEnumerable();
    }


    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Many(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        FilterDefinition<TItem> baseFilter = new BsonDocument(whereClause);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(baseFilter, includeDeleted), effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, pageSize, pageNumber, effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );
    
        return result.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await One(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.One,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            sortOrders: sortOrdersList,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, limit: 1);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TItem>>(
            transaction,
            (collection, session) => collection.FindAsync(session, filter, findOptions, cancellationToken),
            collection => collection.FindAsync(filter, findOptions, cancellationToken)
        );

        return await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> predicate = _ => true;
        var context = BuildReadContext(
            RepositoryReadOperation.All,
            includeDeleted: includeDeleted,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(Builders<TItem>.Filter.Where(predicate), includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Project(selector).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<TProjection> ById<TItem, TProjection>(string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> predicate = item => item.Id == id;
        var context = BuildReadContext(
            RepositoryReadOperation.ById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(Builders<TItem>.Filter.Where(predicate), includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Limit(1).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Limit(1).Project(selector).ToCursorAsync(cancellationToken));

        return await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(IEnumerable<string> IDs, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var idList = IDs.ToList();
        Expression<Func<TItem, bool>> predicate = item => idList.Contains(item.Id);
        var context = BuildReadContext(
            RepositoryReadOperation.ByIds,
            includeDeleted: includeDeleted,
            ids: idList,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        predicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(Builders<TItem>.Filter.Where(predicate), includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Project(selector).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.Many,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: pageSize,
            pageNumber: pageNumber,
            sortOrders: sortOrdersList,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => BuildProjectedFindFluent(collection.Find(session, filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken),
            collection => BuildProjectedFindFluent(collection.Find(filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        FilterDefinition<TItem> baseFilter = new BsonDocument(whereClause);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(baseFilter, includeDeleted), effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => BuildProjectedFindFluent(collection.Find(session, filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken),
            collection => BuildProjectedFindFluent(collection.Find(filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<TProjection> One<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var results = await Many(predicate, selector, continueFrom, 1, null, sortOrders, includeDeleted, transaction, cancellationToken);
        return await results.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IFindFluent<TItem, TProjection> BuildProjectedFindFluent<TItem, TProjection>(
        IFindFluent<TItem, TItem> query,
        Expression<Func<TItem, TProjection>> selector,
        IEnumerable<SortOrder<TItem>>? sortOrders,
        int? pageSize,
        int? pageNumber,
        string? continueFrom) where TItem : Entity
    {
        var sortOrdersList = sortOrders?.ToList();

        if (sortOrdersList is { Count: > 0 })
        {
            query = query.Sort(getSortDefinition(sortOrdersList, null));
        }

        if (pageNumber is > 0 && string.IsNullOrEmpty(continueFrom))
        {
            query = query.Skip((pageNumber.Value - 1) * (pageSize ?? 20));
        }

        if (pageSize.HasValue)
        {
            query = query.Limit(pageSize.Value);
        }

        return query.Project(selector);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var context = BuildReadContext(
            RepositoryReadOperation.Random,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: count,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = predicate == null ? null : ApplyReadBehaviors(predicate, context);
        var aggregate = transaction != null
            ? GetCollection<TItem>().Aggregate(((MongoDbTransactionWrapper)transaction).Session)
            : GetCollection<TItem>().Aggregate();
        
        if (predicate != null || !string.IsNullOrEmpty(continueFrom))
        {
            FilterDefinition<TItem> filter;
            
            if (effectivePredicate != null)
            {
                filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectivePredicate, includeDeleted), continueFrom);
            }
            else
            {
                // If only continueFrom is provided without a predicate, create a filter for continuation
                filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(Builders<TItem>.Filter.Empty, includeDeleted), continueFrom);
            }
            
            aggregate = aggregate.Match(filter);
        }
        else if (!includeDeleted && SupportsSoftDelete<TItem>())
        {
            aggregate = aggregate.Match(BuildNotDeletedFilter<TItem>());
        }

        var result = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));

        return result.ToAsyncEnumerable();
    }
}