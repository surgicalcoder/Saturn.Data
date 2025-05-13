using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.AsyncEnumerable;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IScopedReadonlyRepository
{
    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);

        return scopedEntities;
    }

    public async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        TItem result;

        if (transaction != null)
        {
            result = await (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }

        return result.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (transaction != null)
        {
            return (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable();
        }

        return (await GetCollection<TItem>().FindAsync(e => e.Scope == scope, cancellationToken: cancellationToken)).ToAsyncEnumerable();
    }

    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
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
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, combinedPred, findOptions, cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(combinedPred, findOptions, cancellationToken);
        }

        return await result.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new()) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
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


    public async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);

        long item;

        if (transaction != null)
        {
            item = await GetCollection<TItem>().CountDocumentsAsync(((MongoDBTransactionWrapper)transaction).Session, combinedPred, cancellationToken: cancellationToken);
        }
        else
        {
            item = await GetCollection<TItem>().CountDocumentsAsync(combinedPred, cancellationToken: cancellationToken);
        }

        return item;
    }

    public async Task<TItem> ById<TItem, TScope>(TScope scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        TItem result;

        if (transaction != null)
        {
            result = await (await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            result = await (await GetCollection<TItem>().FindAsync(e => e.Id == id && e.Scope == scope, new FindOptions<TItem> { Limit = 1 }, cancellationToken)).FirstOrDefaultAsync(cancellationToken);
        }

        return result;
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(TScope scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        IAsyncCursor<TItem> result;

        if (transaction != null)
        {
            result = await GetCollection<TItem>().FindAsync(((MongoDBTransactionWrapper)transaction).Session, e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }
        else
        {
            result = await GetCollection<TItem>().FindAsync(e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);
        }


        return result.ToAsyncEnumerable();
    }
}