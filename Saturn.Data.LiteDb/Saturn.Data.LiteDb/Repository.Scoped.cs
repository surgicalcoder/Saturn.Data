using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDb;

public partial class Repository : IScopedRepository
{
    public async Task JsonUpdate<T, T2>(string scope, string id, int version, string json) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        var collection = GetCollection<T>();
        var entity = await collection.FindOneAsync(e => e.Scope == scope && e.Id == id);

        if (entity == null)
        {
            throw new NotSupportedException("Entity not found");
        }

        entity = JsonSerializer.Deserialize<T>(json);

        entity.Version = version;

        var updateResult = await collection.UpdateAsync(entity);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task Delete<T, T2>(string scope, Expression<Func<T, bool>> filter) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        await Delete(filter.And(e => e.Scope == scope));
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
        entity.ForEach(f => f.Scope = scope);
        await UpdateMany(entity);
    }

    public async Task Upsert<T, T2>(string scope, T entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.Scope = scope;
        await Upsert(entity);
    }

    public async Task UpsertMany<T, T2>(string scope, List<T> entity) where T : ScopedEntity<T2> where T2 : Entity, new()
    {
        entity.ForEach(f => f.Scope = scope);
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