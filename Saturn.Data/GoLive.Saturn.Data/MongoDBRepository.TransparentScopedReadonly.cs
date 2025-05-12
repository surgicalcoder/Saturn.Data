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

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : ITransparentScopedReadonlyRepository
{
    async Task<TItem> ITransparentScopedReadonlyRepository.ById<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id, transaction, cancellationToken: cancellationToken);
    }

    async Task<List<TItem>> ITransparentScopedReadonlyRepository.ById<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await System.Linq.AsyncEnumerable.ToListAsync((await ById<TItem, TParent>(scope, IDs, transaction, cancellationToken)), cancellationToken);
    }

    async Task<List<Ref<TItem>>> ITransparentScopedReadonlyRepository.ByRef<TItem, TParent>(List<Ref<TItem>> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<TItem, TParent>(scope, enumerable, cancellationToken: cancellationToken);

        return await System.Linq.AsyncEnumerable.ToListAsync(res.Select(r => new Ref<TItem>(r)), cancellationToken);
    }

    async Task<TItem> ITransparentScopedReadonlyRepository.ByRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem, TParent>(scope, item.Id, transaction, cancellationToken: cancellationToken);
    }

    async Task<Ref<TItem>> ITransparentScopedReadonlyRepository.PopulateRef<TItem, TParent>(Ref<TItem> item, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        item.Item = await ById<TItem, TParent>(scope, item.Id, transaction, cancellationToken: cancellationToken);

        return item;
    }


    IQueryable<TItem> ITransparentScopedReadonlyRepository.IQueryable<TItem, TParent>()
    {
        var scope = options.TransparentScopeProvider(typeof(TParent));
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return scopedEntities;
    }

    Task<IAsyncEnumerable<TItem>> ITransparentScopedReadonlyRepository.All<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider(typeof(TParent));

        return All<TItem, TParent>(scope, transaction, cancellationToken: cancellationToken);
    }

    async Task<TItem> ITransparentScopedReadonlyRepository.One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await One<TItem, TParent>(scope, predicate, sortOrders, transaction, cancellationToken: cancellationToken);
    }

    async Task<TItem> ITransparentScopedReadonlyRepository.Random<TItem, TParent>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        TItem item;

        if (transaction != null)
        {
            item = await GetCollection<TItem>()
                         .Aggregate(((MongoDBTransactionWrapper)transaction).Session)
                         .Match(new BsonDocument("Scope", scope)) // Adding a where clause
                         .AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", 1)))
                         .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            item = await GetCollection<TItem>()
                         .Aggregate()
                         .Match(new BsonDocument("Scope", scope)) // Adding a where clause
                         .AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", 1)))
                         .FirstOrDefaultAsync(cancellationToken);
        }

        return item;
    }

    async Task<IAsyncEnumerable<TItem>> ITransparentScopedReadonlyRepository.Random<TItem, TParent>(int count, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        IAsyncEnumerable<TItem> items;

        if (transaction != null)
        {
            items = GetCollection<TItem>()
                    .Aggregate(((MongoDBTransactionWrapper)transaction).Session)
                    .Match(new BsonDocument("Scope", scope)) // Adding a where clause
                    .AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)))
                    .ToAsyncEnumerable();
        }
        else
        {
            items = GetCollection<TItem>()
                    .Aggregate()
                    .Match(new BsonDocument("Scope", scope)) // Adding a where clause
                    .AppendStage<TItem>(new BsonDocument("$sample", new BsonDocument("size", count)))
                    .ToAsyncEnumerable();
        }

        return items;
    }

    public IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return Many<TItem, TParent>(scope, predicate, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        whereClause.Add("Scope", scope);
        var where = new BsonDocument(whereClause);
        List<BsonDocument> result;

        if (transaction != null)
        {
            result = await (await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<TItem>()).FindAsync(((MongoDBTransactionWrapper)transaction).Session, where, null, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        }
        else
        {
            result = await (await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<TItem>()).FindAsync(where, null, cancellationToken: cancellationToken)).ToListAsync(cancellationToken: cancellationToken);
        }

        return result.Select(f => BsonSerializer.Deserialize<TItem>(f)).ToAsyncEnumerable();
    }

    public IQueryable<TItem> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return Many<TItem, TParent>(scope, predicate, pageSize, pageNumber, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem>(whereClause, transaction: transaction, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        whereClause.Add("Scope", scope);
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

    async Task<long> ITransparentScopedReadonlyRepository.CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        long res;

        if (transaction != null)
        {
            res = await CountMany<TItem, TParent>(scope, predicate, transaction, cancellationToken: cancellationToken);
        }
        else
        {
            res = await CountMany<TItem, TParent>(scope, predicate, cancellationToken: cancellationToken);
        }

        return res;
    }

    Task ITransparentScopedReadonlyRepository.Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}