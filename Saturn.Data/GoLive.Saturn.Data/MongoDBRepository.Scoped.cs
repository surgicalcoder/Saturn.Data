using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IScopedRepository
{
    public async Task JsonUpdate<T, T2>(string Scope, string Id, int Version, string Json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        UpdateResult updateOneAsync;

        if (transaction != null)
        {
            updateOneAsync = await GetCollection<T>().UpdateOneAsync(((MongoDBTransactionWrapper)transaction).Session, e => e.Id == Id && e.Scope == Scope && ((e.Version.HasValue && e.Version <= Version) || !e.Version.HasValue), new JsonUpdateDefinition<T>(Json), cancellationToken: cancellationToken);

        }
        else
        {
            updateOneAsync = await GetCollection<T>().UpdateOneAsync(e => e.Id == Id && e.Scope == Scope && ((e.Version.HasValue && e.Version <= Version) || !e.Version.HasValue), new JsonUpdateDefinition<T>(Json), cancellationToken: cancellationToken);
        }

        if (!updateOneAsync.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Delete<T, T2>(string scope, Expression<Func<T, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(filter.And(e => e.Scope == scope), transaction, cancellationToken);
    }
    public async Task Insert<T, T2>(string scope, T entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Insert(entity, transaction, cancellationToken);
    }

    public async Task InsertMany<T, T2>(string scope, IEnumerable<T> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;
        }
        
        await InsertMany(entities, transaction, cancellationToken);
    }

    public async Task Update<T, T2>(string scope, T entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Update(entity, transaction, cancellationToken);
    }

    public async Task UpdateMany<T, T2>(string scope, List<T> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.ForEach(f=>f.Scope = scope);
        await UpdateMany(entity, transaction, cancellationToken);
    }

    public async Task Upsert<T, T2>(string scope, T entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Upsert(entity, transaction, cancellationToken);
    }

    public async Task UpsertMany<T, T2>(string scope, List<T> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.ForEach(f=>f.Scope = scope);
        await UpsertMany(entity, transaction, cancellationToken);
    }

    public async Task Delete<T, T2>(string scope, T entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(f => f.Scope == scope && f.Id == entity.Id, transaction, cancellationToken);
    }

    public async Task Delete<T, T2>(string scope, string Id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(f => f.Scope == scope && f.Id == Id, transaction, cancellationToken);
    }

    public async Task DeleteMany<T, T2>(string scope, IEnumerable<T> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = scope;
        }

        await DeleteMany(entity, transaction, cancellationToken);
    }

    public async Task DeleteMany<T, T2>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (IDs.Count == 0)
        {
            return;
        }

        if (transaction != null)
        {
            await GetCollection<T>().DeleteManyAsync(((MongoDBTransactionWrapper)transaction).Session, f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken);
        }
        else
        {
            await GetCollection<T>().DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id), cancellationToken: cancellationToken);
        }
    }
}