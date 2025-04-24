using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : ISecondScopedRepository
{
    public async Task<TItem> ById<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IAsyncEnumerable<TItem>> All<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public IQueryable<TItem> IQueryable<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<TItem> One<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<IQueryable<TItem>> Many<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate, int pageSize = 20, int pageNumber = 1, IEnumerable<SortOrder<TItem>> sortOrders = null) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task<long> CountMany<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> predicate) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string Id) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        throw new NotImplementedException();
    }
}
