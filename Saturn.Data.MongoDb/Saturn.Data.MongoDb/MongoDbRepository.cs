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
using Saturn.Data.MongoDb.ExpressionRewriters;
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;

[assembly: InternalsVisibleTo("GoLive.Saturn.Data.MongoDb.Benchmarks")]
[assembly: InternalsVisibleTo("GoLive.Saturn.Data.MongoDb.InternalTests")]
[assembly: InternalsVisibleTo("GoLive.Saturn.Data.MongoDb.Tests")]

namespace Saturn.Data.MongoDb;

public partial class MongoDbRepository : IRepositoryIndexManager
{
    internal MongoDbRepository(RepositoryOptions repositoryOptions, IMongoClient client, MongoDbRepositoryOptions? mongoRepositoryOptions = null)
    {
        initialize(repositoryOptions, mongoRepositoryOptions, client);
    }

    public MongoDbRepository(RepositoryOptions repositoryOptions, MongoDbRepositoryOptions? mongoRepositoryOptions = null)
    {
        initialize(repositoryOptions, mongoRepositoryOptions);
    }

    private void initialize(RepositoryOptions repositoryOptions, MongoDbRepositoryOptions? mongoRepositoryOptions, IMongoClient? existingClient = null)
    {
        options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));
        mongoOptions = mongoRepositoryOptions ?? new MongoDbRepositoryOptions();

        var mongoUrl = new MongoUrl(mongoOptions.ConnectionString ?? throw new ArgumentNullException(nameof(mongoOptions.ConnectionString)));

        client = existingClient ?? new MongoClient(buildClientSettings(mongoUrl, mongoRepositoryOptions));
        mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

        RegisterConventions();
    }

    private MongoClientSettings buildClientSettings(MongoUrl mongoUrl, MongoDbRepositoryOptions? mongoRepositoryOptions)
    {
        var settings = MongoClientSettings.FromUrl(mongoUrl);

        if (mongoRepositoryOptions?.EnableDiagnostics == true)
        {
            settings.ClusterConfigurator = cb =>
            {
                cb.Subscribe(new DiagnosticsActivityEventSubscriber(new InstrumentationOptions
                {
                    CaptureCommandText = mongoRepositoryOptions.CaptureCommandText,
                    ShouldStartActivity = mongoRepositoryOptions.ShouldStartActivity
                }));

                if (mongoOptions.DebugMode)
                {
                    setupCallbacks(cb);
                }
            };
        }
        else if (mongoOptions.DebugMode)
        {
            settings.ClusterConfigurator = setupCallbacks;
        }

        return settings;
    }

    private static CreateIndexModel<TItem> buildCreateIndexModel<TItem>(IIndexDefinition<TItem> definition) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Keys == null || definition.Keys.Count == 0)
        {
            throw new ArgumentException("Index definition must contain at least one key.", nameof(definition));
        }

        IndexKeysDefinition<TItem>? keys = null;

        foreach (var key in definition.Keys)
        {
            ArgumentNullException.ThrowIfNull(key);

            var nextKey = key.Direction == IndexSortDirection.Ascending
                ? Builders<TItem>.IndexKeys.Ascending(key.Field)
                : Builders<TItem>.IndexKeys.Descending(key.Field);

            keys = keys == null
                ? nextKey
                : Builders<TItem>.IndexKeys.Combine(keys, nextKey);
        }

        var options = definition.Options ?? new IndexOptions();

        return new CreateIndexModel<TItem>(
            keys!,
            new CreateIndexOptions
            {
                Name = string.IsNullOrWhiteSpace(definition.Name) ? null : definition.Name,
                Unique = options.Unique,
                Sparse = options.Sparse,
                Background = options.Background,
                ExpireAfter = options.HasExpireAfter ? options.ExpireAfter : null
            });
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        var wrapper = new MongoDbTransactionWrapper(await client.StartSessionAsync());

        return wrapper;
    }

    public async Task EnsureIndexes<TItem>(IEnumerable<IIndexDefinition<TItem>> definitions, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var createIndexModels = definitions
            .Select(buildCreateIndexModel)
            .ToList();

        if (createIndexModels.Count == 0)
        {
            return;
        }

        await GetCollection<TItem>().Indexes.CreateManyAsync(createIndexModels, cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void setupCallbacks(ClusterBuilder cb)
    {
        if (mongoOptions.CommandStartedCallback != null)
        {
            cb.Subscribe<CommandStartedEvent>(e =>
            {
                var document = e.Command.ToJson();
                Task.Run(async () => await mongoOptions.CommandStartedCallback.Invoke(MongoCommandStartedEvent.FromMongoEvent(e, document)));
            });
        }

        if (mongoOptions.CommandFailedCallback != null)
        {
            cb.Subscribe<CommandFailedEvent>(e => { Task.Run(async () => await mongoOptions.CommandFailedCallback.Invoke(MongoCommandFailedEvent.FromMongoEvent(e))); });
        }

        if (mongoOptions.CommandSucceededCallback != null)
        {
            cb.Subscribe<CommandSucceededEvent>(e => { Task.Run(async () => await mongoOptions.CommandSucceededCallback.Invoke(MongoCommandSucceededEvent.FromMongoEvent(e))); });
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
        return typeNameCache.GetOrAdd(typeof(T).FullName ?? typeof(T).Name, _ => options.GetCollectionName.Invoke(typeof(T)));
    }

    protected virtual RepositoryReadContext<TItem> BuildReadContext<TItem>(
        RepositoryReadOperation operation,
        bool includeDeleted = false,
        string? id = null,
        IEnumerable<string>? ids = null,
        string? continueFrom = null,
        int? pageSize = null,
        int? pageNumber = null,
        IEnumerable<SortOrder<TItem>>? sortOrders = null,
        Expression<Func<TItem, bool>>? predicate = null,
        IEnumerable<KeyValuePair<string, object>>? whereClause = null,
        IDatabaseTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        return new RepositoryReadContext<TItem>
        {
            Operation = operation,
            IncludeDeleted = includeDeleted,
            Id = id,
            Ids = ids?.ToList(),
            ContinueFrom = continueFrom,
            PageSize = pageSize,
            PageNumber = pageNumber,
            SortOrders = sortOrders?.ToList(),
            Predicate = predicate,
            WhereClause = whereClause?.ToList(),
            Transaction = transaction,
            CancellationToken = cancellationToken
        };
    }

    protected virtual RepositoryWriteContext<TItem> BuildWriteContext<TItem>(
        RepositoryWriteOperation operation,
        string? id = null,
        IEnumerable<string>? ids = null,
        IEnumerable<TItem>? items = null,
        Expression<Func<TItem, bool>>? filter = null,
        long? expectedVersion = null,
        string? jsonDocument = null,
        IDataUpdateDefinition<TItem>? updateDefinition = null,
        LambdaExpression? incrementField = null,
        object? incrementDelta = null,
        IDatabaseTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        return new RepositoryWriteContext<TItem>
        {
            Operation = operation,
            Id = id,
            Ids = ids?.ToList(),
            Items = items?.ToList(),
            Filter = filter,
            ExpectedVersion = expectedVersion,
            JsonDocument = jsonDocument,
            UpdateDefinition = updateDefinition,
            IncrementField = incrementField,
            IncrementDelta = incrementDelta,
            Transaction = transaction,
            CancellationToken = cancellationToken
        };
    }

    protected virtual Expression<Func<TItem, bool>> ApplyReadBehaviors<TItem>(Expression<Func<TItem, bool>> predicate,
        RepositoryReadContext<TItem> context) where TItem : Entity
    {
        var effectivePredicate = predicate;

        if (options.ReadBehaviors == null)
        {
            return effectivePredicate;
        }

        foreach (var behavior in options.ReadBehaviors)
        {
            effectivePredicate = behavior.BeforeQueryExecution(effectivePredicate, context);
        }

        return effectivePredicate;
    }

    protected virtual IQueryable<TItem> ApplyReadBehaviors<TItem>(IQueryable<TItem> query, RepositoryReadContext<TItem> context)
        where TItem : Entity
    {
        var effectiveQuery = query;

        if (options.ReadBehaviors == null)
        {
            return effectiveQuery;
        }

        foreach (var behavior in options.ReadBehaviors)
        {
            effectiveQuery = behavior.BeforeQueryExecution(effectiveQuery, context);
        }

        return effectiveQuery;
    }

    protected virtual async ValueTask ApplyWriteBehaviors<TItem>(RepositoryWriteOperation operation, RepositoryWriteContext<TItem> context)
        where TItem : Entity
    {
        if (options.WriteBehaviors == null)
        {
            return;
        }

        foreach (var behavior in options.WriteBehaviors)
        {
            switch (operation)
            {
                case RepositoryWriteOperation.Insert:
                    await behavior.BeforeInsert(context);
                    break;
                case RepositoryWriteOperation.Update:
                    await behavior.BeforeUpdate(context);
                    break;
                case RepositoryWriteOperation.Upsert:
                    await behavior.BeforeUpsert(context);
                    break;
                case RepositoryWriteOperation.Save:
                    await behavior.BeforeSave(context);
                    break;
                case RepositoryWriteOperation.Delete:
                    await behavior.BeforeDelete(context);
                    break;
                case RepositoryWriteOperation.HardDelete:
                    await behavior.BeforeHardDelete(context);
                    break;
                case RepositoryWriteOperation.Restore:
                    await behavior.BeforeRestore(context);
                    break;
                case RepositoryWriteOperation.Patch:
                    await behavior.BeforePatch(context);
                    break;
                case RepositoryWriteOperation.Increment:
                    await behavior.BeforeIncrement(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }
    }

    protected virtual void RegisterConventions()
    {
        if (Interlocked.Exchange(ref serializerRegistrationCompleted, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new IgnoreIfDefaultConvention(true),
            new IgnoreEmptyArraysConvention(),
            new IgnoreExtraElementsConvention(true),
            new NamedIdMemberConvention("Id"),
            new IgnoreEmptyStringsConvention(),
            new StringIdStoredAsObjectIdConvention(),
            new AlwaysSerializeEnumsConvention() // Must be after IgnoreIfDefaultConvention to override default behavior for enums
        };

        ConventionRegistry.Register("Custom Conventions", pack, _ => true);

        var objectSerializer = new ObjectSerializer(mongoOptions.ObjectSerializerConfiguration);
        
        _ = BsonSerializer.TryRegisterSerializer(objectSerializer);

        _ = BsonSerializer.TryRegisterSerializer(typeof(WeakRef), new WeakRefSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(Timestamp), new TimestampSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
        _ = BsonSerializer.TryRegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
        
        BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>));
        BsonSerializer.RegisterGenericSerializerDefinition(typeof(WeakRef<>), typeof(WeakRefSerializer<>));

        _ = BsonSerializer.TryRegisterSerializer(typeof(EncryptedString), new EncryptedStringSerializer());
        _ = BsonSerializer.TryRegisterSerializer(typeof(HashedString), new HashedStringSerializer());

        if (mongoOptions.Discriminators is { Count: > 0 })
        {
            foreach (var (key, value) in mongoOptions.Discriminators)
            {
                BsonSerializer.RegisterDiscriminator(value, new BsonString(key));
            }
        }

        if (mongoOptions.DiscriminatorConventions != null)
        {
            foreach (var convention in mongoOptions.DiscriminatorConventions)
            {
                BsonSerializer.RegisterDiscriminatorConvention(convention.Key, convention.Value as IDiscriminatorConvention);
            }
        }

        if (mongoOptions.GenericSerializers != null)
        {
            foreach (var serializer in mongoOptions.GenericSerializers)
            {
                BsonSerializer.RegisterGenericSerializerDefinition(serializer.Key, serializer.Value);
            }
        }

        if (mongoOptions.Serializers != null)
        {
            foreach (var serializer in mongoOptions.Serializers)
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
                   .SetIdGenerator(StringObjectIdGenerator.Instance);
            });
        }
    }

    /// <summary>
    /// Builds a filter definition with optional continuation token support
    /// </summary>
    protected static FilterDefinition<TItem> BuildFilterWithContinuation<TItem>(
        FilterDefinition<TItem> baseFilter, 
        string? continueFrom) where TItem : Entity
    {
        if (string.IsNullOrEmpty(continueFrom))
        {
            return baseFilter;
        }

        if (ObjectId.TryParse(continueFrom, out var objectId))
        {
            return Builders<TItem>.Filter.And(
                baseFilter,
                Builders<TItem>.Filter.Gt("_id", objectId)
            );
        }

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
        string? continueFrom) where TItem : Entity
    {
        var baseFilter = Builders<TItem>.Filter.Where(predicate.NormalizeForRef());
        return BuildFilterWithContinuation(baseFilter, continueFrom);
    }

    protected static Expression<Func<TItem, bool>> ApplySoftDeleteFilter<TItem>(Expression<Func<TItem, bool>> predicate, bool includeDeleted)
        where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return predicate;
        }

        return BuildNotDeletedPredicate<TItem>().And(predicate);
    }

    protected static FilterDefinition<TItem> ApplySoftDeleteFilter<TItem>(FilterDefinition<TItem> filter, bool includeDeleted)
        where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return filter;
        }

        return Builders<TItem>.Filter.And(filter, BuildNotDeletedFilter<TItem>());
    }

    protected static IQueryable<TItem> ApplySoftDeleteFilter<TItem>(IQueryable<TItem> query, bool includeDeleted)
        where TItem : Entity
    {
        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return query;
        }

        return query.Where(BuildNotDeletedPredicate<TItem>());
    }

    protected static Expression<Func<TItem, bool>> BuildNotDeletedPredicate<TItem>() where TItem : Entity
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var body = Expression.Equal(property, Expression.Constant(false));

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    protected static FilterDefinition<TItem> BuildNotDeletedFilter<TItem>() where TItem : Entity
    {
        return Builders<TItem>.Filter.Or(
            Builders<TItem>.Filter.Exists(nameof(ISoftDeletable.IsDeleted), false),
            Builders<TItem>.Filter.Eq(nameof(ISoftDeletable.IsDeleted), false));
    }

    protected static bool CanApplyContinuation<TItem>(IEnumerable<SortOrder<TItem>>? sortOrders) where TItem : Entity
    {
        if (sortOrders == null)
        {
            return true;
        }

        using var enumerator = sortOrders.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return true;
        }

        var firstSort = enumerator.Current;

        return firstSort.Direction == SortDirection.Ascending &&
               GetFieldNameFromExpression(firstSort.Field) == nameof(Entity.Id);
    }

    // Helper to get the field name from an expression
    private static string GetFieldNameFromExpression<TItem, TKey>(Expression<Func<TItem, TKey>> keySelector)
    {
        if (keySelector.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (keySelector.Body is UnaryExpression unary && unary.Operand is MemberExpression member2)
        {
            return member2.Member.Name;
        }
        throw new InvalidOperationException("Invalid key selector expression");
    }

    /// <summary>
    /// Builds FindOptions with pagination and sorting support
    /// </summary>
    protected static FindOptions<TItem> BuildFindOptions<TItem>(
        IEnumerable<SortOrder<TItem>>? sortOrders = null,
        int? pageSize = null,
        int? pageNumber = null,
        string? continueFrom = null,
        int? limit = null) where TItem : Entity
    {
        var findOptions = new FindOptions<TItem>();

        // Handle sorting
        var sortOrdersList = sortOrders?.ToList();
        if (sortOrdersList != null && sortOrdersList.Any())
        {
            findOptions.Sort = getSortDefinition(sortOrdersList, null!);
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

        // Handle pagination (skip-based pagination is incompatible with continuation tokens)
        if (pageNumber is > 0 && string.IsNullOrEmpty(continueFrom))
        {
            findOptions.Skip = (pageNumber.Value - 1) * (pageSize ?? 20);
        }

        return findOptions;
    }

    protected static SortDefinition<T> getSortDefinition<T>(IEnumerable<SortOrder<T>> sortOrders, SortDefinition<T>? sortDefinition) where T : Entity
    {
        var sortOrdersList = sortOrders.ToList();
        
        foreach (var sortOrder in sortOrdersList)
        {
            var sortBuilder = Builders<T>.Sort;
            sortDefinition = sortOrder.Direction == SortDirection.Ascending
                ? sortDefinition == null ? sortBuilder.Ascending(sortOrder.Field) : sortDefinition.Ascending(sortOrder.Field)
                : sortDefinition == null
                    ? sortBuilder.Descending(sortOrder.Field)
                    : sortDefinition.Descending(sortOrder.Field);
        }

        // Always add ID as a secondary sort key if it's not already the primary sort
        var primarySortField = sortOrdersList.FirstOrDefault();
        if (primarySortField != null)
        {
            var primaryFieldName = GetFieldNameFromExpression(primarySortField.Field);
            if (primaryFieldName != "Id" && primaryFieldName != "_id")
            {
                // Add ID as secondary sort in ascending order for consistent pagination
                sortDefinition = sortDefinition?.Ascending(x => x.Id) ?? Builders<T>.Sort.Ascending(x => x.Id);
            }
        }

        return sortDefinition!;
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

    protected RepositoryOptions options { get; set; } = null!;
    protected MongoDbRepositoryOptions mongoOptions { get; set; } = null!;
    protected IMongoDatabase mongoDatabase { get; set; } = null!;
    protected IMongoClient client { get; set; } = null!;
    private static int serializerRegistrationCompleted;

}