using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB.Async;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class Repository : IRepository
{
    private LiteDatabaseAsync database;
    private RepositoryOptions options;
    public Repository(RepositoryOptions repositoryOptions)
    {
        database = new LiteDatabaseAsync(repositoryOptions.ConnectionString);
        options = repositoryOptions;
    }
    
    public void Dispose()
    {
        database?.Dispose();
    }

    public async Task<T> ById<T>(string id, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        return await collection.FindByIdAsync(id);
    }

    private ILiteCollectionAsync<T> getCollection<T>(string overrideCollectionName = "") where T : Entity
    {
        return database.GetCollection<T>(getCollectionNameForType<T>(overrideCollectionName));
    }

    internal string getCollectionNameForType<T>(string collectionNameOverride)
    {
        if (options?.CollectionNameOverride != null)
        {
            return options?.CollectionNameOverride?.Invoke(collectionNameOverride);
        }

        if (string.IsNullOrWhiteSpace(collectionNameOverride))
        {
            var name = typeof(T).Name.AsSpan();

            if (name.Contains(Statics.Separator(), StringComparison.CurrentCulture))
            {
                var genericName = name[..name.IndexOf(Statics.Separator())].ToString();

                if (genericName == "WrappedEntity")
                {
                    var typeArgument = typeof(T).GenericTypeArguments[0].Name;
                    return !string.IsNullOrWhiteSpace(options?.WrappedEntityPrefix) ? $"{options.WrappedEntityPrefix}{typeArgument}" : typeArgument;
                }

                return genericName;
            }

            return name.ToString();
        }

        return collectionNameOverride.Replace(".", "");
    }
    
    public async Task<List<T>> ById<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        return (await collection.FindAsync(e => IDs.Contains(e.Id))).ToList();
    }

    public async Task<List<Ref<T>>> ByRef<T>(List<Ref<T>> Item, string overrideCollectionName = "") where T : Entity, new()
    {
        var enumerable = Item.Where(e => string.IsNullOrWhiteSpace(e.Id)).Select(f => f.Id).ToList();
        var res = await ById<T>(enumerable, overrideCollectionName);
        return res.Select(r => new Ref<T>(r)).ToList();
    }

    public async Task<T> ByRef<T>(Ref<T> Item, string overrideCollectionName = "") where T : Entity, new()
    {
        return string.IsNullOrWhiteSpace(Item.Id) ? null : await ById<T>(Item.Id, overrideCollectionName);
    }

    public async Task<Ref<T>> PopulateRef<T>(Ref<T> Item, string overrideCollectionName = "") where T : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(Item.Id))
        {
            return default;
        }

        Item.Item = await ById<T>(Item.Id, overrideCollectionName);

        return Item;
    }

    public async Task<IQueryable<T>> All<T>(string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        return collection.AsQueryable();
    }

    public async Task<T> One<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        return await collection.FindOneAsync(predicate);
    }

    public async Task<T> Random<T>(string overrideCollectionName = "") where T : Entity
    {
        return (await Random<T>(1, overrideCollectionName)).FirstOrDefault();
    }

    public async Task<List<T>> Random<T>(int count, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        var rnd = new Random();
        var offset = rnd.Next(0, await collection.CountAsync());
        return await collection.Query().Limit(count).Offset(offset).ToListAsync();
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        var pred = predicate.Compile();
        return collection.AsQueryable().Where(e => pred(e));
    }

    public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, string overrideCollectionName = "") where T : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
    {
        throw new NotImplementedException();
    }

    public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        return await collection.CountAsync(predicate);
    }

    public async Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation operationFilter, Action<T, string, ChangeOperation> callback, string overrideCollectionName = "") where T : Entity
    {
        throw new NotImplementedException();
    }

    public async Task Add<T>(T entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        _ = await collection.InsertAsync(entity);
    }

    public async Task AddMany<T>(IEnumerable<T> entities, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        _ = await collection.InsertBulkAsync(entities);
    }

    public async Task Update<T>(T entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.UpdateAsync(entity);
    }

    public async Task UpdateMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.UpdateAsync(entity);
    }

    public async Task Upsert<T>(T entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.UpsertAsync(entity);
    }

    public async Task UpsertMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.UpsertAsync(entity);
    }

    public async Task Delete<T>(T entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.DeleteAsync(entity.Id);
    }

    public async Task Delete<T>(Expression<Func<T, bool>> filter, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.DeleteManyAsync(filter);
    }

    public async Task Delete<T>(string Id, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.DeleteAsync(Id);
    }

    public async Task DeleteMany<T>(IEnumerable<T> entity, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        foreach (var e in entity)
        {
            await collection.DeleteAsync(e.Id); // TODO need to fix  
        }
    }

    public async Task DeleteMany<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity
    {
        var collection = getCollection<T>(overrideCollectionName);
        await collection.DeleteManyAsync(e => IDs.Contains(e.Id));
    }

    public async Task JsonUpdate<T>(string Id, int Version, string Json, string overrideCollectionName = "") where T : Entity
    { 
        throw new NotImplementedException();
    }

    public void InitDatabase()
    {
        
    }
}