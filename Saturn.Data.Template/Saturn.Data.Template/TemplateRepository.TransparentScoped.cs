using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : ITransparentScopedRepository
{
    public async Task Insert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task InsertMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Save<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task SaveMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Update<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task UpdateMany<TItem, TParent>(List<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Upsert<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TParent>(TItem entity) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem, TParent>(string id) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem, TParent>(IEnumerable<TItem> entities) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem, TParent>(List<string> IDs) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    public async Task JsonUpdate<TItem, TParent>(string id, int version, string json) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        throw new NotImplementedException();
    }
    
    async Task ITransparentScopedRepository.UpsertMany<TItem, TParent>(List<TItem> entity)
    {
        throw new NotImplementedException();
    }
}
