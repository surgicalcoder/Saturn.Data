using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    public async Task<TItem> ById<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id);
    }

    public async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return (await ById<TItem, TParent>(scope, IDs)).ToListAsync().Result; // TODO: This is wrong too.
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<TItem, TParent>(scope, enumerable);
        return res.Select(r => new Ref<TItem>(r)).ToListAsync().Result;
    }

    public async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return string.IsNullOrWhiteSpace(item.Id) ? null : await ById<TItem, TParent>(scope, item.Id);
    }

    public async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return default;
        }
        item.Item = await ById<TItem, TParent>(scope, item.Id);
        
        return item;
    }

    
    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(f => f.Scope == scope);
        return scopedEntities;
    }
    
    public Task<IAsyncEnumerable<TItem>> All<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return All<TItem, TParent>(scope);
    }

    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await One<TItem, TParent>(scope, predicate, sortOrders);
    }

    public async Task<TItem> Random<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f=>f.Scope == scope).Take(1).FirstOrDefault();
       
        return item;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f=>f.Scope == scope).Take(count);
        return item.ToAsyncEnumerable();
    }

    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await Many<TItem, TParent>(scope, predicate, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        whereClause.Add("Scope", scope);
        var where = new BsonDocument(whereClause);
        var result = await (await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<TItem>()).FindAsync(where, null)).ToListAsync();
        return result.Select(f => BsonSerializer.Deserialize<TItem>(f)).ToAsyncEnumerable();
    }

    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await Many<TItem, TParent>(scope, predicate, pageSize, pageNumber, sortOrders);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        if (pageSize == 0 || pageNumber == 0)
        {
            return await Many<TItem>(whereClause).ConfigureAwait(false);
        }
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        whereClause.Add("Scope", scope);
        var where = new BsonDocument(whereClause);
        
        var findOptions = new FindOptions<TItem>();
        
        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }
        
        var res = await GetCollection<TItem>().FindAsync(where, findOptions);

        return res.ToAsyncEnumerable();
    }
    
    public async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await CountMany<TItem, TParent>(scope, predicate);
    }

    public async Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
}