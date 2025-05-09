using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : ITransparentScopedRepository
{
    async Task ITransparentScopedRepository.Insert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Insert<TItem, TParent>(scope, entity, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.InsertMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await InsertMany<TItem, TParent>(scope, entities, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.Save<TItem, TParent>(TItem entity, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        entity.Scope = scope;
        await Upsert<TItem, TParent>(scope, entity, transaction, cancellationToken);
    }

    async Task ITransparentScopedRepository.SaveMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpsertMany<TItem, TParent>(scope, entities, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.Update<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Update<TItem, TParent>(scope, entity, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.UpdateMany<TItem, TParent>(List<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entities, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.Upsert<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Upsert<TItem, TParent>(scope, entity, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.UpsertMany<TItem, TParent>(List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await UpdateMany<TItem, TParent>(scope, entity, transaction, cancellationToken: cancellationToken);
    }
    
    async Task ITransparentScopedRepository.Delete<TItem, TParent>(TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, entity, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.Delete<TItem, TParent>(Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await Delete<TItem, TParent>(scope, filter, transaction, cancellationToken: cancellationToken);
    }

    Task ITransparentScopedRepository.Delete<TItem, TParent>(string id, IDatabaseTransaction transaction, CancellationToken cancellationToken)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        return Delete<TItem, TParent>(scope, id, transaction, cancellationToken);
    }

    async Task ITransparentScopedRepository.DeleteMany<TItem, TParent>(List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, IDs, transaction, cancellationToken: cancellationToken);
    }

    async Task ITransparentScopedRepository.JsonUpdate<TItem, TParent>(string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await JsonUpdate<TItem, TParent>(scope, id, version, json, transaction, cancellationToken: cancellationToken);
    }
    
    
    async Task ITransparentScopedRepository.DeleteMany<TItem, TParent>(IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
    {
        var scope = options.TransparentScopeProvider.Invoke(typeof(TParent));
        await DeleteMany<TItem, TParent>(scope, entities, transaction, cancellationToken: cancellationToken);
    }
}