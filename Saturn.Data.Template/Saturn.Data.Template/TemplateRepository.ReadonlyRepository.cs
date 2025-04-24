using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : IReadonlyRepository
{
    public async Task<TItem> ById<TItem>(string id) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem>(List<string> IDs) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<List<Ref<TItem>>> ByRef<TItem>(List<Ref<TItem>> items) where TItem : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task<TItem> ByRef<TItem>(Ref<TItem> item) where TItem : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task<Ref<TItem>> PopulateRef<TItem>(Ref<TItem> item) where TItem : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task<IAsyncEnumerable<TItem>> All<TItem>() where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<TItem> Random<TItem>() where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(int count) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IQueryable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IQueryable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<long> CountMany<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
    {
        throw new NotImplementedException();
    }

    public async Task Watch<TItem>(Expression<Func<ChangedEntity<TItem>, bool>> predicate, ChangeOperation operationFilter, Action<TItem, string, ChangeOperation> callback) where TItem : Entity
    {
        throw new NotImplementedException();
    }
}
