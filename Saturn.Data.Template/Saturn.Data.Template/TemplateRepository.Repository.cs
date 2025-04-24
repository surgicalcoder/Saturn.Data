using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Template;

public partial class TemplateRepository : IRepository
{
    public async Task Insert<TItem>(TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task InsertMany<TItem>(IEnumerable<TItem> entities) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    
    public async Task Save<TItem>(TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task SaveMany<TItem>(List<TItem> entities) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Update<TItem>(TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Update<TItem>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task UpdateMany<TItem>(List<TItem> entities) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Upsert<TItem>(TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task UpsertMany<TItem>(List<TItem> entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem>(TItem entity) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem>(Expression<Func<TItem, bool>> filter) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task Delete<TItem>(string id) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem>(IEnumerable<TItem> entities) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task DeleteMany<TItem>(List<string> IDs) where TItem : Entity
    {
        throw new NotImplementedException();
    }
    public async Task JsonUpdate<TItem>(string id, int version, string json) where TItem : Entity
    {
        throw new NotImplementedException();
    }
}
