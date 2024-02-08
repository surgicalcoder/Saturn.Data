using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;

namespace Saturn.Data.LiteDb;

public partial class Repository : IRepository
{
    public async Task Insert<T>(T entity) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }
        await GetCollection<T>().InsertAsync(entity);
    }

    public async Task InsertMany<T>(IEnumerable<T> entities) where T : Entity
    {
        if (entities == null || !entities.Any())
        {
            return;
        }

        await GetCollection<T>().InsertBulkAsync(entities);
    }

    public async Task Save<T>(T entity) where T : Entity
    {
        await Upsert<T>(entity);
    }

    public async Task SaveMany<T>(List<T> entities) where T : Entity
    {
        await UpsertMany<T>(entities);
    }

    public async Task Update<T>(T entity) where T : Entity
    {
        var updateResult = await GetCollection<T>().UpdateAsync(entity);

        if (!updateResult)
        {
            throw new FailedToUpdateException();
        }
    }

    public async Task UpdateMany<T>(List<T> entities) where T : Entity
    {
        if (entities == null || entities.Count == 0)
        {
            return;
        }

        var coll = GetCollection<T>();

        for (int i = 0; i < entities.Count; i++)
        {
            var res = await coll.UpdateAsync(entities[i]);

            if (!res)
            {
                throw new FailedToUpdateException();
            }
        }
    }

    public async Task Upsert<T>(T entity) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.NewObjectId().ToString();
        }

        var updateResult =  await GetCollection<T>().UpsertAsync(entity); 
            
        if (!updateResult)
        {
            throw new FailedToUpsertException();
        }
    }

    public async Task UpsertMany<T>(List<T> entity) where T : Entity
    {
        if (entity == null || entity.Count == 0)
        {
            return;
        }

        var coll = GetCollection<T>();

        for (int i = 0; i < entity.Count; i++)
        {
            if (string.IsNullOrEmpty(entity[i].Id))
            {
                entity[i].Id = ObjectId.NewObjectId().ToString();
            }

            if (!await coll.UpsertAsync(entity[i]))
            {
                throw new FailedToUpsertException(); // TODO might be worth adding in here which item failed to upsert/update
            }
        }
    }

    public async Task Delete<T>(T entity) where T : Entity
    {
        await GetCollection<T>().DeleteManyAsync(f => f.Id == entity.Id);
    }

    public async Task Delete<T>(Expression<Func<T, bool>> filter) where T : Entity
    {
        await GetCollection<T>().DeleteManyAsync(filter);
    }

    public async Task Delete<T>(string id) where T : Entity
    {
        await Delete<T>(f => f.Id == id);
    }

    public async Task DeleteMany<T>(IEnumerable<T> entities) where T : Entity
    {
        if (!entities.Any())
        {
            return;
        }
        var list = entities.Select(r => r.Id).ToList();

        await GetCollection<T>().DeleteManyAsync(arg => list.Contains(arg.Id));
    }

    public async Task DeleteMany<T>(List<string> IDs) where T : Entity
    {
        if (IDs.Count == 0)
        {
            return;
        }

        await GetCollection<T>().DeleteManyAsync(f => IDs.Contains(f.Id));
    }

    public async Task JsonUpdate<T>(string id, int version, string json) where T : Entity
    {
        throw new NotImplementedException();
    }
}