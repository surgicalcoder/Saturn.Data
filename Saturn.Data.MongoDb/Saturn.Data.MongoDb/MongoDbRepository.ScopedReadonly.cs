using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await All<TItem, TScope>(scope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.All,
            includeDeleted: includeDeleted,
            predicate: scopePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(scopePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await ById<TItem, TScope>(scope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem, TScope>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> basePredicate = e => e.Id == id && e.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.ById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: basePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(basePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await ById<TItem, TScope>(scope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var idList = IDs.ToList();
        Expression<Func<TItem, bool>> basePredicate = e => idList.Contains(e.Id) && e.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.ByIds,
            includeDeleted: includeDeleted,
            ids: idList,
            predicate: basePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(basePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate,  string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Count<TItem, TScope>(scope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPredicate = firstPred.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.Count,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            predicate: combinedPredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combinedPredicate, context);
        var combinedPred = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var filter = BuildFilterWithContinuation(combinedPred, continueFrom);
        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            async (collection, session) => await collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken),
            async collection => await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
        );
    }

    public async Task<bool> Exists<TItem, TScope>(string scope, string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Scope == scope && item.Id == id;
        var context = BuildReadContext(
            RepositoryReadOperation.ExistsById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: predicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(predicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var countOptions = new CountOptions { Limit = 1 };

        var count = await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, countOptions, cancellationToken),
            collection => collection.CountDocumentsAsync(filter, countOptions, cancellationToken)
        );

        return count > 0;
    }

    public async Task<bool> Exists<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var combined = scopePredicate.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.ExistsByPredicate,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);
        var countOptions = new CountOptions { Limit = 1 };

        var count = await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, countOptions, cancellationToken),
            collection => collection.CountDocumentsAsync(filter, countOptions, cancellationToken)
        );

        return count > 0;
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return IQueryable<TItem, TScope>(scope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope, bool includeDeleted) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var context = BuildReadContext<TItem>(RepositoryReadOperation.Queryable, includeDeleted: includeDeleted, predicate: item => item.Scope == scope);
        var query = ApplyReadBehaviors(GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope), context);
        return ApplySoftDeleteFilter(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Many<TItem, TScope>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combined = firstPred.And(predicate);
        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.Many,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: pageSize,
            pageNumber: pageNumber,
            sortOrders: sortOrdersList,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPred = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(combinedPred, effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, pageSize, pageNumber, effectiveContinueFrom);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, findOptions, cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, findOptions, cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await Many<TItem, TScope>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom,
        int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var scopeFilter = Builders<TItem>.Filter.Where(scopePredicate);
        var whereFilter = new BsonDocumentFilterDefinition<TItem>(new BsonDocument(whereClause));
        var baseFilter = Builders<TItem>.Filter.And(scopeFilter, whereFilter);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(baseFilter, includeDeleted), effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, pageSize, pageNumber, effectiveContinueFrom);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, findOptions, cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, findOptions, cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        return await One<TItem, TScope>(scope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combined = firstPred.And(predicate);
        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.One,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            sortOrders: sortOrdersList,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPred = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(combinedPred, effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, limit: 1);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null,  string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new()
    {
        return await Random<TItem, TScope>(scope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopeFilter = item => item.Scope == scope;
        var combinedPredicate = predicate == null ? scopeFilter : scopeFilter.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.Random,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: count,
            predicate: combinedPredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectiveCombinedPredicate = ApplyReadBehaviors(combinedPredicate, context);
    
        FilterDefinition<TItem> filter;
        if (predicate != null)
        {
            var filteredPredicate = ApplySoftDeleteFilter(effectiveCombinedPredicate, includeDeleted);
            filter = BuildFilterWithContinuation(filteredPredicate, continueFrom);
        }
        else
        {
            filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(effectiveCombinedPredicate, includeDeleted), continueFrom);
        }
        
        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) =>
            {
                var aggregate = collection.Aggregate(session);
                if (predicate != null || !string.IsNullOrEmpty(continueFrom))
                {
                    aggregate = aggregate.Match(filter);
                }
                else
                {
                    aggregate = includeDeleted ? aggregate.Match(scopeFilter) : aggregate.Match(ApplySoftDeleteFilter(scopeFilter, false));
                }
                var pipeline = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));
                return pipeline.ToAsyncEnumerable();
            },
            async collection =>
            {
                var aggregate = collection.Aggregate();
                if (predicate != null || !string.IsNullOrEmpty(continueFrom))
                {
                    aggregate = aggregate.Match(filter);
                }
                else
                {
                    aggregate = includeDeleted ? aggregate.Match(scopeFilter) : aggregate.Match(ApplySoftDeleteFilter(scopeFilter, false));
                }
                var pipeline = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));
                return pipeline.ToAsyncEnumerable();
            }
        );
    }

    public async Task<IAsyncEnumerable<TProjection>> All<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.All,
            includeDeleted: includeDeleted,
            predicate: scopePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(scopePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Project(selector).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<TProjection> ById<TItem, TScope, TProjection>(string scope, string id, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> basePredicate = e => e.Id == id && e.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.ById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: basePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(basePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Limit(1).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Limit(1).Project(selector).ToCursorAsync(cancellationToken));

        return await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IAsyncEnumerable<TProjection>> ById<TItem, TScope, TProjection>(string scope, IEnumerable<string> ids,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var idList = ids.ToList();
        Expression<Func<TItem, bool>> basePredicate = e => idList.Contains(e.Id) && e.Scope == scope;
        var context = BuildReadContext(
            RepositoryReadOperation.ByIds,
            includeDeleted: includeDeleted,
            ids: idList,
            predicate: basePredicate,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(basePredicate, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => collection.Find(session, filter).Project(selector).ToCursorAsync(cancellationToken),
            collection => collection.Find(filter).Project(selector).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combined = firstPred.And(predicate);
        var sortOrdersList = sortOrders?.ToList();
        var context = BuildReadContext(
            RepositoryReadOperation.Many,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: pageSize,
            pageNumber: pageNumber,
            sortOrders: sortOrdersList,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPred = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(combinedPred, effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => BuildProjectedFindFluent(collection.Find(session, filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken),
            collection => BuildProjectedFindFluent(collection.Find(filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TProjection>> Many<TItem, TScope, TProjection>(string scope, Dictionary<string, object> whereClause,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var scopeFilter = Builders<TItem>.Filter.Where(scopePredicate);
        var whereFilter = new BsonDocumentFilterDefinition<TItem>(new BsonDocument(whereClause));
        var baseFilter = Builders<TItem>.Filter.And(scopeFilter, whereFilter);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(ApplySoftDeleteFilter(baseFilter, includeDeleted), effectiveContinueFrom);

        var result = await ExecuteWithTransaction<TItem, IAsyncCursor<TProjection>>(
            transaction,
            (collection, session) => BuildProjectedFindFluent(collection.Find(session, filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken),
            collection => BuildProjectedFindFluent(collection.Find(filter), selector, sortOrdersList, pageSize, pageNumber, effectiveContinueFrom).ToCursorAsync(cancellationToken));

        return result.ToAsyncEnumerable();
    }

    public async Task<TProjection> One<TItem, TScope, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : ScopedEntity<TScope>, new()
        where TScope : Entity, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var results = await Many<TItem, TScope, TProjection>(scope, predicate, selector, continueFrom, 1, null, sortOrders, includeDeleted, transaction, cancellationToken);
        return await results.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}

