using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : ITransparentScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<List<TItem>> ById<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<List<Ref<TItem>>> ByRef<TItem, TParent>(List<Ref<TItem>> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> ByRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<Ref<TItem>> PopulateRef<TItem, TParent>(Ref<TItem> item) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public IQueryable<TItem> IQueryable<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> One<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> Random<TItem, TParent>() where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TParent>(int count) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IQueryable<TItem>> Many<TItem, TParent>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TParent>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<long> CountMany<TItem, TParent>(Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Watch<TItem, TParent>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
}
