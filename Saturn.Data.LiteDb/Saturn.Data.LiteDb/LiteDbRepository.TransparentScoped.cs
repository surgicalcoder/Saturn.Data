using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : ITransparentScopedRepository
{
    public async Task Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) 
        where TItem : ScopedEntity<TParent>, new() 
        where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, filter, transaction, cancellationToken);
    }
    
    public async Task Delete<TItem, TParent>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, id, transaction, cancellationToken);
    }
    
    public async Task Delete<TItem, TParent>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, IDs, transaction, cancellationToken);
    }
    public async Task Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Insert<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }
    
    public async Task Insert<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Insert<TItem, TParent>(scope, entities, transaction, cancellationToken);
    }
    
    public async Task JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await JsonUpdate<TItem, TParent>(scope, id, version, json, transaction, cancellationToken);
    }
    
    public async Task Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Save<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }
    
    public async Task Save<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Save<TItem, TParent>(scope, entities, transaction, cancellationToken);
    }
    
    public async Task Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }
    
    public async Task Update<TItem, TParent>(Expression<Func<TItem, bool>> conditionPredicate, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, conditionPredicate, entity, transaction, cancellationToken);
    }
    
    public async Task Update<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, entities, transaction, cancellationToken);
    }
    
    public async Task Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Upsert<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }
    
    public async Task Upsert<TItem, TParent>(IEnumerable<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TParent>, new() where TParent : Entity, new()
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Upsert<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }
    
}