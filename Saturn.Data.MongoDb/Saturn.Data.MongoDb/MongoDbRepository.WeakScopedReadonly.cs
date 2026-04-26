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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, scopePredicate, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(scopePredicate, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<TItem> ById<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => item.Id == id;
        var combinedPredicate = scopePredicate.And(idPredicate);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, combinedPredicate, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(combinedPredicate, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        Expression<Func<TItem, bool>> idPredicate = item => IDs.Contains(item.Id);
        var combinedPredicate = scopePredicate.And(idPredicate);

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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = scopePredicate.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);

        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            async (collection, session) => await collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken),
            async collection => await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
        );
    }

    public IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        return GetCollection<TItem>().AsQueryable().Where(scopePredicate);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new())
        where TItem : Entity, IScopedById, new()
    {
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = scopePredicate.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

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
        var scopedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = scope
        };
        var baseFilter = new BsonDocumentFilterDefinition<TItem>(new BsonDocument(scopedWhereClause));
        var filter = BuildFilterWithContinuation(baseFilter, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);
        var combinedPredicate = scopePredicate.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, limit: 1);

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
        var scopePredicate = ScopeModelHelper.BuildScopePredicate<TItem>(scope);

        FilterDefinition<TItem> filter;
        if (predicate != null)
        {
            var combinedPredicate = scopePredicate.And(predicate);
            filter = BuildFilterWithContinuation(combinedPredicate, continueFrom);
        }
        else
        {
            filter = BuildFilterWithContinuation(scopePredicate, continueFrom);
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
                    aggregate = aggregate.Match(scopePredicate);
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
                    aggregate = aggregate.Match(scopePredicate);
                }

                var pipeline = aggregate.AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)));
                return pipeline.ToAsyncEnumerable();
            }
        );
    }
}

