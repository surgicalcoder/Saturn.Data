using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : ITransparentScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<TItem> ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<long> Count<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        
        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            async (collection, session) => await collection.CountDocumentsAsync(session, combinedPred, cancellationToken: cancellationToken),
            async collection => await collection.CountDocumentsAsync(combinedPred, cancellationToken: cancellationToken)
        );
    }

    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPred, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, findOptions, cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, findOptions, cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
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

    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPred, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, limit: 1);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == scope;
        var combinedPredicate = predicate != null ? scopePredicate.And(predicate) : scopePredicate;
        
        var pipeline = new BsonDocument[]
        {
            new("$match", combinedPredicate.ToBsonDocument()),
            new("$sample", new BsonDocument("size", count))
        };

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.AggregateAsync<TItem>(session, pipeline, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.AggregateAsync<TItem>(pipeline, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }
}