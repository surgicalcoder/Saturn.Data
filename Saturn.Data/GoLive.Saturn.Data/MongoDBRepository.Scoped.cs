﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace GoLive.Saturn.Data;

public partial class MongoDBRepository : IScopedRepository
{
    public async Task JsonUpdate<T, T2>(string Scope, string Id, int Version, string Json) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var updateOneAsync = await GetCollection<T>().UpdateOneAsync(e => e.Id == Id && e.Scope == Scope && ((e.Version.HasValue && e.Version <= Version ) || !e.Version.HasValue), new JsonUpdateDefinition<T>(Json));

        if (!updateOneAsync.IsAcknowledged)
        {
            throw new FailedToUpdateException();
        }
    }
    
    public async Task Delete<T, T2>(string scope, Expression<Func<T, bool>> filter) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(filter.And(e => e.Scope == scope));
    }

    async Task IScopedRepository.UpsertMany<T, T2>(List<T> entity)
    {
        if (entity.Any(e => string.IsNullOrWhiteSpace(e.Scope)))
        {
            throw new FailedToUpsertException();
        }
        await UpsertMany(entity);
    }
    
    public async Task Insert<T, T2>(string scope, T entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Insert(entity);
    }

    public async Task InsertMany<T, T2>(string scope, IEnumerable<T> entities) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;
        }
        
        await InsertMany(entities);
    }

    public async Task Update<T, T2>(string scope, T entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Update(entity);
    }

    public async Task UpdateMany<T, T2>(string scope, List<T> entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.ForEach(f=>f.Scope = scope);
        await UpdateMany(entity);
    }

    public async Task Upsert<T, T2>(string scope, T entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Upsert(entity);
    }

    public async Task UpsertMany<T, T2>(string scope, List<T> entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.ForEach(f=>f.Scope = scope);
        await UpsertMany(entity);
    }

    public async Task Delete<T, T2>(string scope, T entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(f => f.Scope == scope && f.Id == entity.Id);
    }

    public async Task Delete<T, T2>(string scope, string Id) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete<T>(f => f.Scope == scope && f.Id == Id);
    }

    public async Task DeleteMany<T, T2>(string scope, IEnumerable<T> entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        foreach (var scopedEntity in entity)
        {
            scopedEntity.Scope = scope;
        }

        await DeleteMany(entity);
    }

    public async Task DeleteMany<T, T2>(string scope, List<string> IDs) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        if (IDs.Count == 0)
        {
            return;
        }

        await GetCollection<T>().DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id));
    }
}