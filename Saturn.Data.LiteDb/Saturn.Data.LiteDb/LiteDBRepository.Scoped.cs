using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IScopedRepository
{
    public async Task Insert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Insert(entity, cancellationToken: cancellationToken);
    }

    public async Task InsertMany<TItem, TScope>(string scope, IEnumerable<TItem> entities, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        foreach (var scopedEntity in entities)
        {
            scopedEntity.Scope = scope;
        }

        await InsertMany(entities, cancellationToken: cancellationToken);
    }

    public async Task Update<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Update(entity, cancellationToken: cancellationToken);
    }

    public async Task UpdateMany<TItem, TScope>(string scope, List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.ForEach(f => f.Scope = scope);
        await UpdateMany(entity, cancellationToken: cancellationToken);
    }

    public async Task JsonUpdate<TItem, TScope>(string scope, string id, int version, string json, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
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

    public async Task Upsert<TItem, TScope>(string scope, TItem entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.Scope = scope;
        await Upsert(entity, cancellationToken: cancellationToken);
    }

    public async Task UpsertMany<TItem, TScope>(string scope, List<TItem> entity, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        entity.ForEach(f => f.Scope = scope);
        await UpsertMany(entity, cancellationToken: cancellationToken);
    }

    public async Task Delete<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        await Delete<TItem>(f => f.Scope == scope && f.Id == id, cancellationToken: cancellationToken);
    }

    public async Task Delete<TItem, TScope>(string scope, Expression<Func<TItem, bool>> filter, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        await Delete(filter.And(e => e.Scope == scope), cancellationToken: cancellationToken);
    }

    public async Task DeleteMany<TItem, TScope>(string scope, List<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope> where TScope : Entity, new()
    {
        if (IDs.Count == 0)
        {
            return;
        }

        await GetCollection<TItem>().DeleteManyAsync(f => f.Scope == scope && IDs.Contains(f.Id));
    }
}