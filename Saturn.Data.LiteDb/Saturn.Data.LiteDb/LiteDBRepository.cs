using System.Collections.Concurrent;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using LiteDB.Async;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IRepository
{
    protected LiteDatabaseAsync database;
    protected RepositoryOptions options;
    protected LiteDBRepositoryOptions liteDbOptions;
    
    public LiteDBRepository(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions )
    {
        this.liteDbOptions = liteDbRepositoryOptions;
        database = new LiteDatabaseAsync(repositoryOptions.ConnectionString, liteDbRepositoryOptions.Mapper);
        
        /*var allEntityTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(r => r.GetTypes())
            .Where(t => typeof(Entity).IsAssignableFrom(t) && t != typeof(Entity));
        
        allEntityTypes = allEntityTypes.Where(t => t.IsClass && !t.IsAbstract);
        
        foreach (var allEntityType in allEntityTypes)
        {
            var refType = typeof(Ref<>).MakeGenericType(allEntityType);
            BsonMapper.Global.RegisterType(refType, LiteDBRefSerializer.Serialize(allEntityType), LiteDBRefSerializer.Deserialize(refType));
        }*/
        
        options = repositoryOptions;
    }
    
    public void Dispose()
    {
        database?.Dispose();
    }
    
    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();
    
    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }
    
    protected virtual ILiteCollectionAsync<T> GetCollection<T>() where T : Entity
    {
        return database.GetCollection<T>(GetCollectionNameForType<T>());
    }
    
    public async Task InitDatabase()
    {
        await options?.InitCallback?.Invoke(this);
    }
}