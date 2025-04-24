using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : IScopedReadonlyRepository
{
    public async Task<TItem> ById<TItem, TScope>(TScope scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(TScope scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> ById<TItem, TScope>(string scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, List<string> IDs) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IQueryable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, int pageSize, int pageNumber, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<long> CountMany<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
}
