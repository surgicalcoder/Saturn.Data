using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.EntitySerializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Saturn.Data.MongoDb.Callbacks;
using Saturn.Data.MongoDb.Conventions;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

[assembly: InternalsVisibleTo("GoLive.Saturn.Data.MongoDb.Benchmarks")]
[assembly: InternalsVisibleTo("GoLive.Saturn.Data.MongoDb.InternalTests")]

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository
{
    internal MongoDbRepository(RepositoryOptions repositoryOptions, IMongoClient client, MongoDbRepositoryOptions mongoRepositoryOptions = null)
    {
        options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));
        mongoOptions = mongoRepositoryOptions ?? new MongoDbRepositoryOptions();
        var connectionString = mongoOptions.ConnectionString ?? throw new ArgumentNullException("mongoOptions.ConnectionString");

        var mongoUrl = new MongoUrl(connectionString);

        var settings = MongoClientSettings.FromUrl(mongoUrl);

        if (mongoRepositoryOptions?.EnableDiagnostics == true)
        {
            settings.ClusterConfigurator = cb =>
            {
                cb.Subscribe(new DiagnosticsActivityEventSubscriber(new InstrumentationOptions
                {
                    CaptureCommandText = mongoRepositoryOptions?.CaptureCommandText ?? false,
                    ShouldStartActivity = mongoRepositoryOptions?.ShouldStartActivity
                }));

                if (mongoOptions is { DebugMode: true })
                {
                    setupCallbacks(cb);
                }
            };
        }
        else
        {
            if (mongoOptions is { DebugMode: true })
            {
                settings.ClusterConfigurator = setupCallbacks;
            }
        }

        this.client = client;

        mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

        RegisterConventions();
    }

    public MongoDbRepository(RepositoryOptions repositoryOptions, MongoDbRepositoryOptions mongoRepositoryOptions = null)
    {
        options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));
        mongoOptions = mongoRepositoryOptions;

        var connectionString = mongoOptions.ConnectionString ?? throw new ArgumentNullException("mongoOptions.ConnectionString");

        var mongoUrl = new MongoUrl(connectionString);

        var settings = MongoClientSettings.FromUrl(mongoUrl);

        if (mongoRepositoryOptions?.EnableDiagnostics == true)
        {
            settings.ClusterConfigurator = cb =>
            {
                cb.Subscribe(new DiagnosticsActivityEventSubscriber(new InstrumentationOptions
                {
                    CaptureCommandText = mongoRepositoryOptions?.CaptureCommandText ?? false,
                    ShouldStartActivity = mongoRepositoryOptions?.ShouldStartActivity
                }));

                if (mongoOptions is { DebugMode: true })
                {
                    setupCallbacks(cb);
                }
            };
        }
        else
        {
            if (mongoOptions is { DebugMode: true })
            {
                settings.ClusterConfigurator = setupCallbacks;
            }
        }

        client = new MongoClient(settings);

        mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

        RegisterConventions();
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        var wrapper = new MongoDbTransactionWrapper(await client.StartSessionAsync());

        return wrapper;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void setupCallbacks(ClusterBuilder cb)
    {
        if (mongoOptions?.CommandStartedCallback != null)
        {
            cb.Subscribe<CommandStartedEvent>(e => { mongoOptions?.CommandStartedCallback?.Invoke(new MongoCommandStartedEvent { Command = e.CommandName.ToJson(), CommandName = e.CommandName, RequestId = e.RequestId }); });
        }

        if (mongoOptions?.CommandFailedCallback != null)
        {
            cb.Subscribe<CommandFailedEvent>(e => { mongoOptions?.CommandFailedCallback.Invoke(new MongoCommandFailedEvent{ CommandName = e.CommandName, RequestId = e.RequestId, Failure = e.Failure }); });
        }

        if (mongoOptions?.CommandSucceededCallback != null)
        {
            cb.Subscribe<CommandSucceededEvent>(e => { mongoOptions?.CommandSucceededCallback.Invoke(new MongoCommandSucceededEvent{ CommandName = e.CommandName, RequestId = e.RequestId, Duration = e.Duration }); });
        }


        if (mongoOptions?.CommandSucceededCallback != null)
        {
            cb.Subscribe<CommandSucceededEvent>(e => { Task.Run(async () => await mongoOptions.CommandSucceededCallback.Invoke(MongoCommandSucceededEvent.FromMongoEvent(e))); });
        }

        if (mongoOptions?.CommandStartedCallback != null)
        {
            cb.Subscribe<CommandStartedEvent>(e =>
            {
                var document = e.Command.ToJson();
                Task.Run(async () => await mongoOptions.CommandStartedCallback.Invoke(MongoCommandStartedEvent.FromMongoEvent(e, document)));
            });
        }

        if (mongoOptions?.CommandFailedCallback != null)
        {
            cb.Subscribe<CommandFailedEvent>(e => { Task.Run(async () => await mongoOptions.CommandFailedCallback.Invoke(MongoCommandFailedEvent.FromMongoEvent(e))); });
        }
    }

    public void Dispose(bool val)
    {
        if (val)
        {
            client.Dispose();
        }
    }

    protected virtual IMongoCollection<T> GetCollection<T>() where T : Entity
    {
        return mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>());
    }

    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }

    protected virtual void RegisterConventions()
    {
        var pack = new ConventionPack
        {
            new IgnoreIfDefaultConvention(true),
            new IgnoreEmptyArraysConvention(),
            new IgnoreExtraElementsConvention(true),
            new NamedIdMemberConvention("Id"),
            new IgnoreEmptyStringsConvention(),
            new StringIdStoredAsObjectIdConvention()
        };

        ConventionRegistry.Register("Custom Conventions", pack, t => true);

        var objectSerializer = new ObjectSerializer(mongoOptions.ObjectSerializerConfiguration);
        _ = BsonSerializer.TryRegisterSerializer(objectSerializer);

        _ = BsonSerializer.TryRegisterSerializer(typeof(Timestamp), new TimestampSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

        BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>));
        BsonSerializer.RegisterGenericSerializerDefinition(typeof(WeakRef<>), typeof(WeakRefSerializer<>));

        _ = BsonSerializer.TryRegisterSerializer(typeof(EncryptedString), new EncryptedStringSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(HashedString), new HashedStringSerializer());

        if (mongoOptions?.Discriminators is { Count: > 0 })
        {
            foreach (var (key, value) in mongoOptions.Discriminators)
            {
                BsonSerializer.RegisterDiscriminator(value, new BsonString(key));
            }
        }

        if (mongoOptions?.DiscriminatorConventions != null)
        {
            foreach (var convention in mongoOptions?.DiscriminatorConventions)
            {
                BsonSerializer.RegisterDiscriminatorConvention(convention.Key, convention.Value as IDiscriminatorConvention);
            }
        }

        if (mongoOptions?.GenericSerializers != null)
        {
            foreach (var serializer in mongoOptions?.GenericSerializers)
            {
                BsonSerializer.RegisterGenericSerializerDefinition(serializer.Key, serializer.Value);
            }
        }

        if (mongoOptions?.Serializers != null)
        {
            foreach (var serializer in mongoOptions?.Serializers)
            {
                BsonSerializer.RegisterSerializer(serializer.Key, serializer.Value as IBsonSerializer);
            }
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Entity)))
        {
            BsonClassMap.RegisterClassMap(delegate(BsonClassMap<Entity> map)
            {
                map.AutoMap();

                map.MapProperty(f => f.Version).SetElementName("_v");
                map.UnmapProperty(f => f.EnableChangeTracking);
                map.UnmapProperty(f => f.Changes);

                map.MapProperty(e => e.Properties).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, dynamic>>(DictionaryRepresentation.ArrayOfDocuments)).SetElementName("_p");

                map.IdMemberMap.SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId))
                   .SetIdGenerator(StringObjectIdGenerator.Instance)
                   .SetIgnoreIfDefault(true)
                   .SetDefaultValue(() => ObjectId.GenerateNewId().ToString());
            });
        }
    }

    /// <summary>
    /// Builds a filter definition with optional continuation token support
    /// </summary>
    protected static FilterDefinition<TItem> BuildFilterWithContinuation<TItem>(
        FilterDefinition<TItem> baseFilter, 
        string continueFrom) where TItem : Entity
    {
        if (string.IsNullOrEmpty(continueFrom))
            return baseFilter;

        return Builders<TItem>.Filter.And(
            baseFilter,
            Builders<TItem>.Filter.Gt(x => x.Id, continueFrom)
        );
    }

    /// <summary>
    /// Builds a filter definition from a predicate with optional continuation token support
    /// </summary>
    protected static FilterDefinition<TItem> BuildFilterWithContinuation<TItem>(
        Expression<Func<TItem, bool>> predicate, 
        string continueFrom) where TItem : Entity
    {
        var baseFilter = Builders<TItem>.Filter.Where(predicate);
        return BuildFilterWithContinuation(baseFilter, continueFrom);
    }

    /// <summary>
    /// Builds FindOptions with pagination and sorting support
    /// </summary>
    protected static FindOptions<TItem> BuildFindOptions<TItem>(
        IEnumerable<SortOrder<TItem>> sortOrders = null,
        int? pageSize = null,
        int? pageNumber = null,
        string continueFrom = null,
        int? limit = null) where TItem : Entity
    {
        var findOptions = new FindOptions<TItem>();

        // Handle sorting
        if (sortOrders != null && sortOrders.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrders, null);
        }

        // Handle limit (takes precedence over pageSize)
        if (limit.HasValue)
        {
            findOptions.Limit = limit.Value;
        }
        else if (pageSize.HasValue)
        {
            findOptions.Limit = pageSize.Value;
        }

        // Handle pagination (only when not using continuation tokens)
        if (pageNumber is > 0 && string.IsNullOrEmpty(continueFrom))
        {
            findOptions.Skip = (pageNumber.Value - 1) * (pageSize ?? 20);
        }

        return findOptions;
    }

    protected static SortDefinition<T> getSortDefinition<T>(IEnumerable<SortOrder<T>> sortOrders, SortDefinition<T> sortDefinition) where T : Entity
    {
        foreach (var sortOrder in sortOrders)
        {
            var sortBuilder = Builders<T>.Sort;
            sortDefinition = sortOrder.Direction == SortDirection.Ascending
                ? sortDefinition == null ? sortBuilder.Ascending(sortOrder.Field) : sortDefinition.Ascending(sortOrder.Field)
                : sortDefinition == null
                    ? sortBuilder.Descending(sortOrder.Field)
                    : sortDefinition.Descending(sortOrder.Field);
        }

        return sortDefinition;
    }
    
    internal virtual async Task<TResult> ExecuteWithTransaction<TItem, TResult>(
        IDatabaseTransaction transaction,
        Func<IMongoCollection<TItem>, IClientSessionHandle, Task<TResult>> withTransactionFunc,
        Func<IMongoCollection<TItem>, Task<TResult>> withoutTransactionFunc) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        if (transaction != null)
        {
            var session = ((MongoDbTransactionWrapper)transaction).Session;
            return await withTransactionFunc(collection, session);
        }

        return await withoutTransactionFunc(collection);
    }

    internal virtual async Task ExecuteWithTransaction<TItem>(
        IDatabaseTransaction transaction,
        Func<IMongoCollection<TItem>, IClientSessionHandle, Task> withTransactionFunc,
        Func<IMongoCollection<TItem>, Task> withoutTransactionFunc) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        if (transaction != null)
        {
            var session = ((MongoDbTransactionWrapper)transaction).Session;
            await withTransactionFunc(collection, session);
        }
        else
        {
            await withoutTransactionFunc(collection);
        }
    }

    protected static RepositoryOptions options { get; set; }
    protected static MongoDbRepositoryOptions mongoOptions { get; set; }
    public static bool InitRun { get; set; }
    public static DateTime InitLastChecked { get; set; }
    protected IMongoDatabase mongoDatabase { get; set; }
    protected IMongoClient client { get; set; }

}