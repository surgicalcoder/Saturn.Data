using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : IScopedRepository
{
    public async Task Insert<TItem, TScope>(string scope, TItem entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task InsertMany<TItem, TScope>(string scope, IEnumerable<TItem> entities) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Update<TItem, TScope>(string scope, TItem entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task UpdateMany<TItem, TScope>(string scope, List<TItem> entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Upsert<TItem, TScope>(string scope, TItem entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }

    public async Task UpsertMany<TItem, TScope>(string scope, List<TItem> entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TScope>(string scope, TItem entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TScope>(string scope, string id) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem, TScope>(string scope, IEnumerable<TItem> entity) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem, TScope>(string scope, List<string> ds) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        throw new NotImplementedException();
    }



    async Task IScopedRepository.UpsertMany<TItem, TParent>(List<TItem> entity)
    {
        await UpsertMany(entity);
    }
}
