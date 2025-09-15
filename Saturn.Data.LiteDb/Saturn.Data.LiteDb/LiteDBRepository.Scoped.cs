using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IScopedRepository
{
    public async Task Delete<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        await GetCollection<TItem>().DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id));
    }
    async Task IScopedRepository.Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        entity.Scope = scope;
        
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().InsertAsync(entity);
    }
    public async Task Insert<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;

            if (string.IsNullOrWhiteSpace(scopedEntity.Id))
            {
                scopedEntity.Id = ObjectId.NewObjectId().ToString();
            }
        }
        await GetCollection<TItem>().InsertAsync(entities);
    }
    
    async Task IScopedRepository.JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var collection = GetCollection<TItem>();
        var entity = await collection.FindOneAsync(e => e.Scope == scope && e.Id == id);

        if (entity == null)
        {
            throw new NotSupportedException("Entity not found");
        }

        entity = JsonSerializer.Deserialize<TItem>(json);

        entity.Version = version;

        var updateResult = await collection.UpdateAsync(entity);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Save<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) 
        where TItem : ScopedEntity<TScope>, new() 
        where TScope : Entity, new()
    {
        entity.Scope = scope;

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }
        await Upsert(entity, cancellationToken: cancellationToken);
    }
    
    public async Task Save<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;
            if (string.IsNullOrWhiteSpace(scopedEntity.Id))
            {
                scopedEntity.Id = ObjectId.NewObjectId().ToString();
            }
        }
        
        await Upsert(entities, transaction, cancellationToken);
    }
    
    async Task IScopedRepository.Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        entity.Scope = scope;
        await Update(entity, cancellationToken: cancellationToken);
    }
    public async Task Update<TItem, TScope>(string scope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Update(conditionPredicate.And(e => e.Scope == scope), entity, cancellationToken: cancellationToken);
    }
    public async Task Update<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = scope;
        }
        await Update(entity, cancellationToken: cancellationToken);
    }
    
    async Task IScopedRepository.Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        entity.Scope = scope;
        await Upsert(entity, cancellationToken: cancellationToken);
    }
    
    public async Task Upsert<TItem, TScope>(string scope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = scope;
        }
        await Upsert(entity, cancellationToken: cancellationToken);
    }

    async Task IScopedRepository.Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        await Delete(filter.And<TItem>(r=>r.Scope == scope), transaction, cancellationToken);
    }

    async Task IScopedRepository.Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        await Delete<TItem>(f => f.Id == id && f.Scope == scope , cancellationToken: cancellationToken);
    }
}