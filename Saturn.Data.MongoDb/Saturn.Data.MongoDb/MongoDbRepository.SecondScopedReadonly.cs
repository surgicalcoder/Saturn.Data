using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : ISecondScopedReadonlyRepository
{
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, e => e.Scope == Scope.Id && e.SecondScope == secondScope.Id, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(e => e.Scope == Scope.Id && e.SecondScope == secondScope.Id, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<TItem> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, e => e.Id == id && e.Scope == Scope.Id && e.SecondScope == secondScope.Id, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(e => e.Id == id && e.Scope == Scope.Id && e.SecondScope == secondScope.Id, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, e => IDs.Contains(e.Id) && e.Scope == Scope.Id && e.SecondScope == secondScope.Id, cancellationToken: cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(e => IDs.Contains(e.Id) && e.Scope == Scope.Id && e.SecondScope == secondScope.Id, cancellationToken: cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<long> Count<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate,  string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = scopePred.And(predicate);
        
        return await ExecuteWithTransaction<TItem, long>(
            transaction,
            async (collection, session) => await collection.CountDocumentsAsync(session, combinedPred, cancellationToken: cancellationToken),
            async collection => await collection.CountDocumentsAsync(combinedPred, cancellationToken: cancellationToken)
        );
    }

    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        return GetCollection<TItem>().AsQueryable().Where(f => f.Scope == Scope.Id && f.SecondScope == secondScope.Id);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = scopePred.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPred, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, pageSize, pageNumber, continueFrom);

        return await ExecuteWithTransaction<TItem, IAsyncEnumerable<TItem>>(
            transaction,
            async (collection, session) => (await collection.FindAsync(session, filter, findOptions, cancellationToken)).ToAsyncEnumerable(),
            async collection => (await collection.FindAsync(filter, findOptions, cancellationToken)).ToAsyncEnumerable()
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        var scopedWhereClause = new Dictionary<string, object>(whereClause)
        {
            ["Scope"] = Scope.Id,
            ["SecondScope"] = secondScope.Id
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

    public async Task<TItem> One<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePred = item => item.Scope == Scope.Id && item.SecondScope == secondScope.Id;
        var combinedPred = scopePred.And(predicate);
        var filter = BuildFilterWithContinuation(combinedPred, continueFrom);
        var findOptions = BuildFindOptions(sortOrders, limit: 1);

        return await ExecuteWithTransaction<TItem, TItem>(
            transaction,
            async (collection, session) => await (await collection.FindAsync(session, filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken),
            async collection => await (await collection.FindAsync(filter, findOptions, cancellationToken)).FirstOrDefaultAsync(cancellationToken)
        );
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TSecondScope, TScope>(Ref<TScope> Scope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate = null,  string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TScope>, new() where TSecondScope : Entity, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> scopePredicate = item => item.Scope == Scope.Id && item.SecondScope == secondScope.Id;
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