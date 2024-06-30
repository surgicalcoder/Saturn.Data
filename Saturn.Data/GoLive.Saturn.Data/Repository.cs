using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Conventions;
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

[assembly: InternalsVisibleTo("GoLive.Saturn.Data.Benchmarks")]
[assembly: InternalsVisibleTo("GoLive.Saturn.InternalTests")]

namespace GoLive.Saturn.Data;

public partial class Repository
{
    #region Props
    protected static RepositoryOptions options { get; set; }
    public static bool InitRun { get; set; }
    public static DateTime InitLastChecked { get; set; }
    protected IMongoDatabase mongoDatabase { get; set; }
    protected IMongoClient client { get; set; }

    #endregion

    internal Repository(RepositoryOptions repositoryOptions, IMongoClient client)
    {
        options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));

        var connectionString = options.ConnectionString ?? throw new ArgumentNullException("repositoryOptions.ConnectionString");

        var mongoUrl = new MongoUrl(connectionString);

        var settings = MongoClientSettings.FromUrl(mongoUrl);

        if (options is { DebugMode: true })
        {
            settings.ClusterConfigurator = setupCallbacks;
        }

        this.client = client;

        mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

        RegisterConventions();
    }

    public Repository(RepositoryOptions repositoryOptions)
    {
        options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));

        var connectionString = options.ConnectionString ?? throw new ArgumentNullException("repositoryOptions.ConnectionString");

        var mongoUrl = new MongoUrl(connectionString);

        var settings = MongoClientSettings.FromUrl(mongoUrl);
            
        if (options is { DebugMode: true })
        {
            settings.ClusterConfigurator = setupCallbacks;
        }

        client = new MongoClient(settings);
            
        mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

        RegisterConventions();
    }

    protected virtual void setupCallbacks(ClusterBuilder cb)
    {
        if (options?.CommandStartedCallback != null)
        {
            cb.Subscribe<CommandStartedEvent>(e =>
            {
                options?.CommandStartedCallback?.Invoke(new CommandStartedArgs(){Command = e.CommandName.ToJson(), CommandName = e.CommandName, RequestId = e.RequestId});
            });
        }

        if (options?.CommandFailedCallback != null)
        {
            cb.Subscribe<CommandFailedEvent>(e =>
            {
                options?.CommandFailedCallback.Invoke(new CommandFailedArgs(){CommandName = e.CommandName, RequestId = e.RequestId, Exception = e.Failure});
            });
        }

        if (options?.CommandCompletedCallback != null)
        {
            cb.Subscribe<CommandSucceededEvent>(e =>
            {
                options?.CommandCompletedCallback.Invoke(new CommandCompletedArgs() {CommandName = e.CommandName, RequestId = e.RequestId, Time = e.Duration});
            });
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool val)
    {
        if (val) { }
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    protected virtual IMongoCollection<T> GetCollection<T>() where T : Entity
    {
        return mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>());
    }
        
    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }

    private void RegisterConventions()
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
        
        var objectSerializer = new ObjectSerializer(options.ObjectSerializerConfiguration);
        _ = BsonSerializer.TryRegisterSerializer(objectSerializer);

        _ = BsonSerializer.TryRegisterSerializer(typeof(Timestamp), new TimestampSerializer());
        _ = 
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

        BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>));
        BsonSerializer.RegisterGenericSerializerDefinition(typeof(WeakRef<>), typeof(WeakRefSerializer<>));

        _ = BsonSerializer.TryRegisterSerializer(typeof(EncryptedString), new EncryptedStringSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(HashedString), new HashedStringSerializer());

        if (options?.Discriminators is { Count: > 0 })
        {
            foreach (var (key, value) in options.Discriminators)
            {
                BsonSerializer.RegisterDiscriminator(value, new BsonString(key));
            }
        }

        if (options?.DiscriminatorConventions != null)
        {
            foreach (var convention in options?.DiscriminatorConventions)
            {
                BsonSerializer.RegisterDiscriminatorConvention(convention.Key, convention.Value as IDiscriminatorConvention);
            }

        }
        if (options?.GenericSerializers != null)
        {
            foreach (var serializer in options?.GenericSerializers)
            {
                BsonSerializer.RegisterGenericSerializerDefinition(serializer.Key, serializer.Value);
            }
        }

        if (options?.Serializers != null)
        {
            foreach (var serializer in options?.Serializers)
            {
                BsonSerializer.RegisterSerializer(serializer.Key, serializer.Value as IBsonSerializer);
            }
        }
        
        if (!BsonClassMap.IsClassMapRegistered(typeof(Entity)))
        {
            BsonClassMap.RegisterClassMap<Entity>(delegate (BsonClassMap<Entity> map)
            {
                map.AutoMap();

                map.MapProperty(f => f.Version).SetElementName("_v");
                map.UnmapProperty(f=>f.EnableChangeTracking);
                map.UnmapProperty(f=>f.Changes);

                map.MapProperty(e => e.Properties).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, dynamic>>(DictionaryRepresentation.ArrayOfDocuments)).SetElementName("_p");

                map.IdMemberMap.SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId))
                    .SetIdGenerator(StringObjectIdGenerator.Instance)
                    .SetIgnoreIfDefault(true)
                    .SetDefaultValue(()=> ObjectId.GenerateNewId().ToString());
            });
        }
    }
}