using System.Collections.Concurrent;
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
    
    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();
    
    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }
    
    private ILiteCollectionAsync<T> GetCollection<T>() where T : Entity
    {
        return database.GetCollection<T>(GetCollectionNameForType<T>());
    }
    
    public async Task InitDatabase()
    {
        await options?.InitCallback?.Invoke(this);
    }
}