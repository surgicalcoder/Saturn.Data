using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IWeakScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await All<TItem>(scope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
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

    public async Task<TItem> ById<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await ById<TItem>(scope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string scope, string id, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;
        var combined = scopePredicate.And(idPredicate);
        var context = BuildReadContext(
            RepositoryReadOperation.ById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, combinedPredicate, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(combinedPredicate, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await ById<TItem>(scope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var idList = IDs.ToList();
        Expression<Func<TItem, bool>> idPredicate = item => idList.Contains(item.Id);
        var combined = scopePredicate.And(idPredicate);
        var context = BuildReadContext(
            RepositoryReadOperation.ByIds,
            includeDeleted: includeDeleted,
            ids: idList,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, combinedPredicate, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(combinedPredicate, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await Count<TItem>(scope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = scopePredicate.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.Count,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);

        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            async (collection, session) => await collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken),
            async collection => await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
        );
    }

    public async Task<bool> Exists<TItem>(string scope, string id, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;
        var combined = scopePredicate.And(idPredicate);
        var context = BuildReadContext(
            RepositoryReadOperation.ExistsById,
            includeDeleted: includeDeleted,
            id: id,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var countOptions = new CountOptions { Limit = 1 };

        var count = await ExecuteWithTransaction<TItem, long>(
            transaction,
            (collection, session) => collection.CountDocumentsAsync(session, filter, countOptions, cancellationToken),
            collection => collection.CountDocumentsAsync(filter, countOptions, cancellationToken)
        );

        return count > 0;
    }

    public async Task<bool> Exists<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
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

    public IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new()
    {
        return IQueryable<TItem>(scope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope, bool includeDeleted)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var context = BuildReadContext<TItem>(RepositoryReadOperation.Queryable, includeDeleted: includeDeleted, predicate: scopePredicate);
        var query = ApplyReadBehaviors(GetCollection<TItem>().AsQueryable().Where(scopePredicate), context);
        return ApplySoftDeleteFilter(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await Many<TItem>(scope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = scopePredicate.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.Many,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            pageSize: pageSize,
            pageNumber: pageNumber,
            sortOrders: sortOrders,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(combinedPredicate, effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, pageSize, pageNumber, effectiveContinueFrom);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, findOptions, cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, findOptions, cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await Many<TItem>(scope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom, int? pageSize,
        int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = scope
        };
        var baseFilter = new BsonDocumentFilterDefinition<TItem>(new BsonDocument(scopedWhereClause));
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

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await One<TItem>(scope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combined = scopePredicate.And(predicate);
        var context = BuildReadContext(
            RepositoryReadOperation.One,
            includeDeleted: includeDeleted,
            continueFrom: continueFrom,
            sortOrders: sortOrders,
            predicate: combined,
            transaction: transaction,
            cancellationToken: cancellationToken);
        var effectivePredicate = ApplyReadBehaviors(combined, context);
        var combinedPredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        var sortOrdersList = sortOrders?.ToList();
        var effectiveContinueFrom = CanApplyContinuation(sortOrdersList) ? continueFrom : null;
        var filter = BuildFilterWithContinuation(combinedPredicate, effectiveContinueFrom);
        var findOptions = BuildFindOptions(sortOrdersList, limit: 1);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        return await Random<TItem>(scope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom, int count,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = predicate == null ? scopePredicate : scopePredicate.And(predicate);
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
                    aggregate = includeDeleted ? aggregate.Match(scopePredicate) : aggregate.Match(ApplySoftDeleteFilter(scopePredicate, false));
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
                    aggregate = includeDeleted ? aggregate.Match(scopePredicate) : aggregate.Match(ApplySoftDeleteFilter(scopePredicate, false));
                }

                var pipeline = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));
                return pipeline.ToAsyncEnumerable();
            }
        );
    }

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(string scope, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        Expression<Func<TItem, bool>> predicate = item => true;
        return Many(scope, predicate, selector, null, null, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<TProjection> ById<TItem, TProjection>(string scope, string id, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Id == id;
        return One(scope, predicate, selector, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(string scope, IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var idList = ids.ToList();
        Expression<Func<TItem, bool>> predicate = idList.Count == 0
            ? item => false
            : item => idList.Contains(item.Id);
        int? pageSize = idList.Count == 0 ? null : idList.Count;

        return Many(scope, predicate, selector, null, pageSize, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return Many(scopePredicate.And(predicate), selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string scope, Dictionary<string, object> whereClause,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var combinedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = scope
        };

        return Many<TItem, TProjection>(combinedWhereClause, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<TProjection> One<TItem, TProjection>(string scope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return One(scopePredicate.And(predicate), selector, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
}

