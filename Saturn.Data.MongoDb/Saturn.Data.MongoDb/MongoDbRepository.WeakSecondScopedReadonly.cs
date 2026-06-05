using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IWeakSecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await All<TItem>(primaryScope, secondScope, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope).And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await ById<TItem>(primaryScope, secondScope, id, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await ById<TItem>(primaryScope, secondScope, IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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
        var filter = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await Count<TItem>(primaryScope, secondScope, predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task<bool> Exists<TItem>(string primaryScope, string secondScope, string id, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task<bool> Exists<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new()
    {
        return IQueryable<TItem>(primaryScope, secondScope, includeDeleted: false);
    }

    public IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope, bool includeDeleted)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        var context = BuildReadContext<TItem>(RepositoryReadOperation.Queryable, includeDeleted: includeDeleted, predicate: scopePredicate);
        var query = ApplyReadBehaviors(GetCollection<TItem>().AsQueryable().Where(scopePredicate), context);
        return ApplySoftDeleteFilter(query, includeDeleted);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction,
            cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        return await Many<TItem>(primaryScope, secondScope, whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction,
            cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause,
        string continueFrom, int? pageSize, int? pageNumber, IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = primaryScope,
            ["SecondScope"] = secondScope
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

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await One<TItem>(primaryScope, secondScope, predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom,
        IEnumerable<SortOrder<TItem>> sortOrders, bool includeDeleted, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new()) where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null,
        string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        return await Random<TItem>(primaryScope, secondScope, predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        string continueFrom, int count, bool includeDeleted, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
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

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        Expression<Func<TItem, bool>> predicate = item => true;
        return Many(primaryScope, secondScope, predicate, selector, null, null, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<TProjection> ById<TItem, TProjection>(string primaryScope, string secondScope, string id,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        Expression<Func<TItem, bool>> predicate = item => item.Id == id;
        return One(primaryScope, secondScope, predicate, selector, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(string primaryScope, string secondScope, IEnumerable<string> ids,
        Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var idList = ids.ToList();
        Expression<Func<TItem, bool>> predicate = idList.Count == 0
            ? item => false
            : item => idList.Contains(item.Id);
        int? pageSize = idList.Count == 0 ? null : idList.Count;

        return Many(primaryScope, secondScope, predicate, selector, null, pageSize, null, null, includeDeleted, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return Many(scopePredicate.And(predicate), selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(string primaryScope, string secondScope,
        Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector, string continueFrom = null,
        int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var combinedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = primaryScope,
            ["SecondScope"] = secondScope
        };

        return Many<TItem, TProjection>(combinedWhereClause, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction,
            cancellationToken);
    }

    public Task<TProjection> One<TItem, TProjection>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate,
        Expression<Func<TItem, TProjection>> selector, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(primaryScope)
            .And(ScopeModelHelper.BuildSecondScopePredicate<TItem>(secondScope));
        return One(scopePredicate.And(predicate), selector, continueFrom, sortOrders, includeDeleted, transaction, cancellationToken);
    }
}

