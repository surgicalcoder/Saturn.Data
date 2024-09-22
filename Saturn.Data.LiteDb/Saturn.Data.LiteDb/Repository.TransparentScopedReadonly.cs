using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class Repository : ITransparentScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, id);
    }

    public async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await ById<TItem, TParent>(scope, IDs);
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var enumerable = item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<TItem, TParent>(scope, enumerable);
        return res.Select(r => new Ref<TItem>(r)).ToList();
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

    public IQueryable<TItem> All<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
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

    public async Task<List<TItem>> Random<TItem, TParent>(int count) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        var item = GetCollection<TItem>().AsQueryable().Where(f=>f.Scope == scope).Take(count);
        return item.ToList();
    }

    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await Many<TItem, TParent>(scope, predicate, sortOrders);
    }

    public Task<List<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await Many<TItem, TParent>(scope, predicate, pageSize, pageNumber, sortOrders);
    }

    public Task<List<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    
    public async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));

        return await CountMany<TItem, TParent>(scope, predicate);
    }

    public Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
}