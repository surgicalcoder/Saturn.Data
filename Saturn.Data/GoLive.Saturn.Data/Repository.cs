using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
using MongoDB.Driver.Linq;

[assembly: InternalsVisibleTo("GoLive.Saturn.Data.Benchmarks")]
[assembly: InternalsVisibleTo("GoLive.Saturn.InternalTests")]

namespace GoLive.Saturn.Data
{
    public partial class Repository : IRepository
    {
        #region Props
        private static RepositoryOptions options { get; set; }

        public static bool InitRun { get; set; }
        public static DateTime InitLastChecked { get; set; }

        private IMongoDatabase mongoDatabase { get; set; }
        private IMongoClient client { get; set; }

        #endregion

        internal Repository(RepositoryOptions repositoryOptions, IMongoClient client)
        {
            options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));

            string connectionString = options.ConnectionString ?? throw new ArgumentNullException("repositoryOptions.ConnectionString");

            var mongoUrl = new MongoUrl(connectionString);

            var settings = MongoClientSettings.FromUrl(mongoUrl);

            if (options != null && options.DebugMode)
            {
                settings.ClusterConfigurator = setupCallbacks;
            }

            this.client = client;

            mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

            RegisterConventions();
            InitDatabase();
        }

        public Repository(RepositoryOptions repositoryOptions)
        {
            options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));

            string connectionString = options.ConnectionString ?? throw new ArgumentNullException("repositoryOptions.ConnectionString");

            var mongoUrl = new MongoUrl(connectionString);

            var settings = MongoClientSettings.FromUrl(mongoUrl);
            
            if (options != null && options.DebugMode)
            {
                settings.ClusterConfigurator = setupCallbacks;
            }

            client = new MongoClient(settings);
            
            mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

            RegisterConventions();
            InitDatabase();
        }

        private static void setupCallbacks(ClusterBuilder cb)
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

        public async Task Delete<T>(string Id, string overrideCollectionName = "") where T : Entity
        {
            await Delete<T>(f => f.Id == Id);
        }

        public async Task DeleteMany<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity
        {
            if (IDs.Count == 0)
            {
                return;
            }

            await GetCollection<T>(overrideCollectionName).DeleteManyAsync(f => IDs.Contains(f.Id));
        }

        public async Task JsonUpdate<T>(string Id, int Version, string Json, string overrideCollectionName = "") where T : Entity
        {
            var updateOneAsync = await GetCollection<T>(overrideCollectionName).UpdateOneAsync(e => e.Id == Id && ((e.Version.HasValue && e.Version <= Version ) || !e.Version.HasValue), new JsonUpdateDefinition<T>(Json));

            if (!updateOneAsync.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }

        public async Task DeleteMany<T>(IEnumerable<T> entity, string overrideCollectionName = "") where T : Entity
        {
            if (!entity.Any())
            {
                return;
            }
            var list = entity.Select(r => r.Id).ToList();

            await GetCollection<T>(overrideCollectionName).DeleteManyAsync(arg => list.Contains(arg.Id));
        }

        private void RegisterConventions()
        {
            var pack = new ConventionPack
            {
                new IgnoreIfDefaultConvention(true),
                new IgnoreEmptyArraysConvention(),
                new IgnoreExtraElementsConvention(true),
                new NamedIdMemberConvention("Id"),
                new StringObjectIdIdGeneratorConvention(),
                new IgnoreEmptyStringsConvention()
            };

            ConventionRegistry.Register("Custom Conventions", pack, t => true);
            
            try
            {
                BsonSerializer.RegisterSerializer(typeof(Timestamp), new TimestampSerializer());

                BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
                BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

                BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>));
                BsonSerializer.RegisterGenericSerializerDefinition(typeof(WeakRef<>), typeof(WeakRefSerializer<>));

                BsonSerializer.RegisterSerializer(typeof(EncryptedString), new EncryptedStringSerializer());
                BsonSerializer.RegisterSerializer(typeof(HashedString), new HashedStringSerializer());
            }
            catch (ArgumentException bsex) when (bsex.Message == "There is already a serializer registered for type Timestamp") { }

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

                    map.MapProperty(e => e.Properties).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, dynamic>>(DictionaryRepresentation.ArrayOfDocuments)).SetElementName("_p");

                    map.IdMemberMap.SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId))
                        .SetIdGenerator(StringObjectIdGenerator.Instance)
                        .SetIgnoreIfDefault(true)
                        .SetDefaultValue(()=> ObjectId.GenerateNewId().ToString());
                });
            }
        }

        public void InitDatabase()
        {
            PopulateDatabase();
        }

        private void PopulateDatabase()
        {
            if (options?.InitDuration == null)
            {
                return;
            }
            if (InitRun && InitLastChecked > DateTime.Now.Add(options.InitDuration))
            {
                return;
            }

            InitRun = true;
            InitLastChecked = DateTime.Now;
            options?.InitCallback?.Invoke(this);
        }

        internal string GetCollectionNameForType<T>(string collectionNameOverride)
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

        #region Get
        
        public async Task<T> ById<T>(string id, string collectionName) where T : Entity
        {
            var result = await (await GetCollection<T>(collectionName).FindAsync(e => e.Id == id, new FindOptions<T> { Limit = 1 })).FirstOrDefaultAsync();

            return result;
        }

        public async Task<List<T>> ById<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity
        {
            var result = await GetCollection<T>(overrideCollectionName).FindAsync(e => IDs.Contains(e.Id));
            return await result.ToListAsync().ConfigureAwait(false);
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

        public async Task Watch<T>(Expression<Func<ChangedEntity<T>, bool>> predicate, ChangeOperation op, Action<T, string, ChangeOperation> callback, string overrideCollectionName = "") where T : Entity
        {
            var pipelineDefinition = new EmptyPipelineDefinition<ChangeStreamDocument<T>>();

            var expression = Converter<ChangeStreamDocument<T>>.Convert(predicate);

            var opType = (ChangeStreamOperationType) op;

            var definition = pipelineDefinition.Match(expression).Match(e=>e.OperationType == opType);

            await GetCollection<T>(overrideCollectionName).WatchAsync(definition);

            var collection = GetCollection<T>(overrideCollectionName);

            using (var asyncCursor = await collection.WatchAsync(pipelineDefinition))
            {
                foreach (var changeStreamDocument in asyncCursor.ToEnumerable())
                {
                    callback.Invoke(changeStreamDocument.FullDocument, changeStreamDocument?.DocumentKey[0]?.AsObjectId.ToString(), (ChangeOperation) changeStreamDocument.OperationType );
                }
            }
        }

        public async Task<T> One<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            var result = await GetCollection<T>(overrideCollectionName).FindAsync(predicate, new FindOptions<T> { Limit = 1 });

            return await result.FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public async Task<T> Random<T>(string overrideCollectionName = "") where T : Entity
        {
            var item = await GetCollection<T>(overrideCollectionName).AsQueryable().Sample(1).FirstOrDefaultAsync();

            return item;
        }
        
        public async Task<List<T>> Random<T>(int count, string overrideCollectionName = "") where T : Entity
        {
            var item = await GetCollection<T>(overrideCollectionName).AsQueryable().Sample(1).ToListAsync();

            return item;
        }

        public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, string overrideCollectionName = "") where T : Entity
        {
            var where = new BsonDocument(WhereClause);

            var result = await (await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>(overrideCollectionName)).FindAsync(where, null)).ToListAsync();
            
            return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
        }

        public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T>(predicate, overrideCollectionName).ConfigureAwait(false);
            }

            return await Task.Run(() => Queryable.Take(GetCollection<T>(overrideCollectionName).AsQueryable().Where(predicate).Skip((PageNumber - 1) * pageSize), pageSize)).ConfigureAwait(false);
        }

        public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T>(WhereClause, overrideCollectionName).ConfigureAwait(false);
            }

            var where = new BsonDocument(WhereClause);
            var result = await(await mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>(overrideCollectionName)).FindAsync(where, new FindOptions<BsonDocument>()
            {
                Skip = (PageNumber - 1) * pageSize,
                Limit = pageSize,
            } )).ToListAsync();

            return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
        }

        private IMongoCollection<T> GetCollection<T>(string collectionName = null) where T : Entity
        {
            return mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(collectionName));
        }

        public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            return await GetCollection<T>(overrideCollectionName).CountDocumentsAsync(predicate);
        }


        public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            return await Task.Run(() => Queryable.Where(GetCollection<T>(overrideCollectionName).AsQueryable(), predicate));
        }

        public async Task<IQueryable<T>> All<T>(string overrideCollectionName = "") where T : Entity
        {
            return await Task.Run(() => GetCollection<T>(overrideCollectionName).AsQueryable()).ConfigureAwait(false);
        }

        #endregion

        #region Add

        public async Task Add<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            await GetCollection<T>(overrideCollectionName).InsertOneAsync(entity);
        }

        public async Task AddMany<T>(IEnumerable<T> entities, string overrideCollectionName = "") where T : Entity
        {
            if (entities == null || !entities.Any())
            {
                return;
            }

            await GetCollection<T>(overrideCollectionName).InsertManyAsync(entities, new InsertManyOptions() { IsOrdered = true });
        }

        #endregion

        #region Delete

        public async Task UpsertMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity
        {
            if (entity == null || entity.Count == 0)
            {
                return;
            }

            for (int i = 0; i < entity.Count; i++)
            {
                if (string.IsNullOrEmpty(entity[i].Id))
                {
                    entity[i].Id = ObjectId.GenerateNewId().ToString();
                }
            }

            //foreach (var x1 in entity.Where(e => string.IsNullOrWhiteSpace(e.Id)))
            //{
            //    x1.Id = ObjectId.GenerateNewId().ToString();
            //}

            var bulkWriteResult = await GetCollection<T>(overrideCollectionName).BulkWriteAsync(entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = true }), new BulkWriteOptions(){IsOrdered = false});

            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }
        }

        public async Task Delete<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            await GetCollection<T>(overrideCollectionName).DeleteOneAsync(f => f.Id == entity.Id);
        }

        public async Task Delete<T>(Expression<Func<T, bool>> filter, string overrideCollectionName = "") where T : Entity
        {
            await GetCollection<T>(overrideCollectionName).DeleteManyAsync(filter);
        }


        #endregion

        #region Update

        public async Task UpdateMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity
        {
            if (entity == null || entity.Count == 0)
            {
                return;
            }

            var writeModel = entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = false });

            var bulkWriteResult = await GetCollection<T>(overrideCollectionName).BulkWriteAsync(writeModel, new BulkWriteOptions() { IsOrdered = false });
            
            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }

        public async Task Upsert<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = ObjectId.GenerateNewId().ToString();
            }
            
            var updateResult = await GetCollection<T>(overrideCollectionName).ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true });
            
            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }
        }

        public async Task Update<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            var updateResult = await GetCollection<T>(overrideCollectionName).ReplaceOneAsync(e => e.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false });

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }

        #endregion


    }
 
}
