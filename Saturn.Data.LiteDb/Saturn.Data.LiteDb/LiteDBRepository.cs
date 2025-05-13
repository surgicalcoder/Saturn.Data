using System.Collections.Concurrent;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using LiteDB.Async;

namespace Saturn.Data.LiteDb;

public partial class LiteDBRepository : IRepository
{
    protected LiteDatabaseAsync database;
    protected LiteDBRepositoryOptions liteDbOptions;
    protected RepositoryOptions options;

    public LiteDBRepository(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    {
        liteDbOptions = liteDbRepositoryOptions;
        var mapper = liteDbRepositoryOptions.Mapper;

        RegisterAllEntityRefs(mapper);

        database = new LiteDatabaseAsync(repositoryOptions.ConnectionString, mapper);
        options = repositoryOptions;
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public void Dispose()
    {
        database?.Dispose();
    }

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException();
    }

    protected virtual void RegisterAllEntityRefs(BsonMapper mapper)
    {
        var entityBase = typeof(Entity);
        var openRef = typeof(Ref<>);

        // 1) find every non-abstract Entity subclass with a public parameterless ctor
        var entityTypes = AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(a =>
                                   {
                                       try
                                       {
                                           return a.GetTypes();
                                       }
                                       catch
                                       {
                                           return Array.Empty<Type>();
                                       }
                                   })
                                   .Where(t =>
                                       entityBase.IsAssignableFrom(t) &&
                                       !t.IsAbstract &&
                                       t.GetConstructor(Type.EmptyTypes) != null
                                   );

        foreach (var entityType in entityTypes)
        {
            // 2) construct the Ref<ThatEntity> type
            var refType = openRef.MakeGenericType(entityType);
            var idProp = refType.GetProperty("Id")!;

            // 3) serializer: take your Ref<ThatEntity>.Id → raw BsonValue
            Func<object, BsonValue> serialize = obj =>
            {
                var idVal = idProp.GetValue(obj);

                return BsonMapper.Global.Serialize(idVal.GetType(), idVal);
            };

            // 4) deserializer: raw BsonValue → new Ref<ThatEntity> { Id = … }
            Func<BsonValue, object> deserialize = bson =>
            {
                var inst = Activator.CreateInstance(refType)!;
                var clrId = BsonMapper.Global.Deserialize(idProp.PropertyType, bson);
                idProp.SetValue(inst, clrId);

                return inst;
            };

            // 5) register it
            mapper.RegisterType(refType, serialize, deserialize);
        }
    }

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