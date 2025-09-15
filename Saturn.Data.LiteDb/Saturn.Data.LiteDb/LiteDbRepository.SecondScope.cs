using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : ISecondScopedRepository
{
    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        await GetCollection<TItem>().DeleteManyAsync(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id && f.Id == id);
    }
    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var combinedPredicate = filter.And<TItem>(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id);
        await GetCollection<TItem>().DeleteManyAsync(combinedPredicate);
    }
    
    public async Task Delete<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        await GetCollection<TItem>().DeleteManyAsync(f => f.Scope == primaryScope.Id && f.SecondScope == secondScope.Id && IDs.Contains(f.Id));
    }
    
    public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope.Id;
        entity.SecondScope = secondScope.Id;
        
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().InsertAsync(entity);
    }
    
    public async Task Insert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = primaryScope.Id;
            scopedEntity.SecondScope = secondScope.Id;

            if (string.IsNullOrWhiteSpace(scopedEntity.Id))
            {
                scopedEntity.Id = ObjectId.NewObjectId().ToString();
            }
        }
        await GetCollection<TItem>().InsertAsync(entity);
    }
    
    public async Task JsonUpdate<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        var collection = GetCollection<TItem>();
        var entity = await collection.FindOneAsync(e => e.Scope == primaryScope.Id && e.SecondScope == secondScope.Id && e.Id == id);

        if (entity == null)
        {
            throw new NotSupportedException("Entity not found");
        }

        entity = System.Text.Json.JsonSerializer.Deserialize<TItem>(json);

        entity.Version = version;

        var updateResult = await collection.UpdateAsync(entity);
        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Save<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope.Id;
        entity.SecondScope = secondScope.Id;
        
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().UpsertAsync(entity);
    }
    
    public async Task Save<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = primaryScope.Id;
            scopedEntity.SecondScope = secondScope.Id;

            if (string.IsNullOrWhiteSpace(scopedEntity.Id))
            {
                scopedEntity.Id = ObjectId.NewObjectId().ToString();
            }
        }
    
        await GetCollection<TItem>().UpsertAsync(entity);
    }
    
    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope;
        entity.SecondScope = secondScope;
        await Update(entity, cancellationToken: cancellationToken);
    }
    
    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope;
        entity.SecondScope = secondScope;
        await Update(conditionPredicate.And(e => e.Scope == primaryScope && e.SecondScope == secondScope ), entity, cancellationToken: cancellationToken);
    }
    
    public async Task Update<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = primaryScope;
            scopedEntity.SecondScope = secondScope;
        }
        await Update(entity, cancellationToken: cancellationToken);
    }
    
    public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        entity.Scope = primaryScope.Id;
        entity.SecondScope = secondScope.Id;
        
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        await GetCollection<TItem>().UpsertAsync(entity);
    }
    
    public async Task Upsert<TItem, TSecondScope, TPrimaryScope>(Ref<TPrimaryScope> primaryScope, Ref<TSecondScope> secondScope, IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : SecondScopedEntity<TSecondScope, TPrimaryScope>, new() where TSecondScope : Entity, new() where TPrimaryScope : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = primaryScope.Id;
            scopedEntity.SecondScope = secondScope.Id;
        
            if (string.IsNullOrWhiteSpace(scopedEntity.Id))
            {
                scopedEntity.Id = ObjectId.NewObjectId().ToString();
            }
        }
    
        await GetCollection<TItem>().UpsertAsync(entity);
    }
}