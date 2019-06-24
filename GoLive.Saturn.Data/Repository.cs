using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Conventions;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.EntitySerializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json.Linq;

namespace GoLive.Saturn.Data
{
    public class Repository : IRepository
    {
        #region Props
        private static RepositoryOptions options { get; set; }

        public static bool InitRun { get; set; }
        public static DateTime InitLastChecked { get; set; }

        private IMongoDatabase mongoDatabase { get; set; }
        private MongoClient client { get; set; }

        #endregion

        public Repository(RepositoryOptions repositoryOptions)
        {
            options = repositoryOptions ?? throw new ArgumentNullException(nameof(repositoryOptions));

            string connectionString = options.ConnectionString ?? throw new ArgumentNullException("repositoryOptions.ConnectionString");

            var mongoUrl = new MongoUrl(connectionString);

            var settings = MongoClientSettings.FromUrl(mongoUrl);
            
            if (options != null && options.DebugMode)
            {
                settings.ClusterConfigurator = cb =>
                {
                    if (options?.CommandStartedCallback != null)
                    {
                        cb.Subscribe<CommandStartedEvent>(e =>
                        {
                            options?.CommandStartedCallback?.Invoke(e.RequestId, e.CommandName, e.Command.ToJson());
                        });
                    }

                    if (options?.CommandFailedCallback != null)
                    {
                        cb.Subscribe<CommandFailedEvent>(e =>
                        {
                            options?.CommandFailedCallback.Invoke(e.RequestId, e.CommandName, e.Failure);
                        });
                    }

                    if (options?.CommandCompletedCallback != null)
                    {
                        cb.Subscribe<CommandSucceededEvent>(e =>
                        {
                            options?.CommandCompletedCallback.Invoke(e.RequestId, e.CommandName, e.Duration);
                        });
                    }
                };
            }

            client = new MongoClient(settings);
            
            mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);

            RegisterConventions();
            InitDatabase();
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

            var collection = GetCollection<T>(overrideCollectionName);

            await collection.DeleteManyAsync(f => IDs.Contains(f.Id));

        }

        public async Task DeleteMany<T>(IEnumerable<T> entity, string overrideCollectionName = "") where T : Entity
        {
            if (!entity.Any())
            {
                return;
            }
            var collection = GetCollection<T>(overrideCollectionName);
            var list = entity.Select(r => r.Id).ToList();

            await collection.DeleteManyAsync(arg => list.Contains(arg.Id));
        }

        private void RegisterConventions()
        {
            var pack = new ConventionPack
            {
                new IgnoreIfDefaultConvention(true),
                new IgnoreEmptyArraysConvention(),
                new IgnoreExtraElementsConvention(true),
                new NamedIdMemberConvention("Id"),
                new StringObjectIdIdGeneratorConvention()
            };

            ConventionRegistry.Register("Custom Conventions", pack, t => true);

            try
            {
                BsonSerializer.RegisterDiscriminatorConvention(typeof(JObject), new JObjectDiscriminatorConvention());

                BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>));
                BsonSerializer.RegisterGenericSerializerDefinition(typeof(WeakRef<>), typeof(WeakRefSerializer<>));

                BsonSerializer.RegisterSerializer(typeof(EncryptedString), new EncryptedStringSerializer());
                BsonSerializer.RegisterSerializer(typeof(HashedString), new HashedStringSerializer());
                BsonSerializer.RegisterSerializer(typeof(JObject), new JObjectSerializer());

                BsonSerializer.RegisterSerializer(typeof(Timestamp), new TimestampSerializer());
            }
            catch (BsonSerializationException bsex) when (bsex.Message == "There is already a discriminator convention registered for type Newtonsoft.Json.Linq.JObject.") { }

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

                    map.IdMemberMap.SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId))
                        .SetIdGenerator(StringObjectIdGenerator.Instance)
                        .SetIgnoreIfDefault(true);
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

        private string GetCollectionNameForType<T>(string collectionNameOverride)
        {
            if (options?.CollectionNameOverride != null)
            {
                return options?.CollectionNameOverride?.Invoke(collectionNameOverride);
            }

            if (string.IsNullOrWhiteSpace(collectionNameOverride))
            {
                var name = typeof(T).Name.AsSpan();

                var genericSeparator = "`".AsSpan();

                if (name.Contains(genericSeparator, StringComparison.CurrentCulture))
                {
                    return name.Slice(0, name.IndexOf(genericSeparator)).ToString();
                }

                return name.ToString();
            }
            else
            {
                return collectionNameOverride.Replace(".", "");
            }
        }


        #region Get

        public async Task<T> ById<T>(string id, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);

            var result = await collection.FindAsync(e => e.Id == id, new FindOptions<T> { Limit = 1 });

            return await result.FirstOrDefaultAsync();
        }

        public async Task<List<T>> ById<T>(List<string> IDs, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);
            var result = await collection.FindAsync(e => IDs.Contains(e.Id));
            return await result.ToListAsync();
        }

        public async Task<List<Ref<T>>> ByRef<T>(List<Ref<T>> Item, string overrideCollectionName = "") where T : Entity
        {
            var enumerable = Item.Where(e => e != null).Select(f => f.Id).ToList();
            var res = await ById<T>(enumerable, overrideCollectionName);

            return res.Select(r => new Ref<T>(r)).ToList();
        }

        public async Task<T> ByRef<T>(Ref<T> Item, string overrideCollectionName = "") where T : Entity
        {
            return Item == null ? null : await ById<T>(Item.Id, overrideCollectionName);
        }

        public async Task<Ref<T>> PopulateRef<T>(Ref<T> Item, string overrideCollectionName = "") where T : Entity
        {
            if (Item == null || string.IsNullOrWhiteSpace(Item.Id))
            {
                return null;
            }

            Item.Item = await ById<T>(Item.Id, overrideCollectionName);

            return Item;
        }

        public async Task<T> One<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);

            var result = await collection.FindAsync(predicate, new FindOptions<T> { Limit = 1 });

            return await result.FirstOrDefaultAsync();
        }


        public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, string overrideCollectionName = "") where T : Entity
        {
            var collection = mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>(overrideCollectionName));
            var where = new BsonDocument(WhereClause);
            var result = await (await collection.FindAsync(where, null)).ToListAsync();

            return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
        }

        public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T>(predicate, overrideCollectionName);
            }
            
            var collection = GetCollection<T>(overrideCollectionName);
            
            return await Task.Run(() => collection.AsQueryable().Where(predicate).Skip((PageNumber - 1) * pageSize).Take(pageSize));
        }

        public async Task<List<T>> Many<T>(Dictionary<string, object> WhereClause, int pageSize, int PageNumber, string overrideCollectionName = "") where T : Entity
        {
            if (pageSize == 0 || PageNumber == 0)
            {
                return await Many<T>(WhereClause, overrideCollectionName);
            }

            var collection = mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>(overrideCollectionName));
            var where = new BsonDocument(WhereClause);
            var result = await(await collection.FindAsync(where, new FindOptions<BsonDocument>()
            {
                Skip = (PageNumber - 1) * pageSize,
                Limit = pageSize,
            } )).ToListAsync();

            return result.Select(f => BsonSerializer.Deserialize<T>(f)).ToList();
        }

        private IMongoCollection<T> GetCollection<T>(string overrideCollectionName) where T : Entity
        {
            return mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(overrideCollectionName));
        }

        public async Task<long> CountMany<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);
            return await collection.CountDocumentsAsync(predicate);
        }


        public async Task<IQueryable<T>> Many<T>(Expression<Func<T, bool>> predicate, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);
            return await Task.Run(() => collection.AsQueryable().Where(predicate));
        }

        public async Task<IQueryable<T>> All<T>(string overrideCollectionName = "") where T : Entity
        {
            return await Task.Run(() => GetCollection<T>(overrideCollectionName).AsQueryable());
        }

        #endregion

        #region Add

        public async Task Add<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);

            await collection.InsertOneAsync(entity);
        }

        public async Task AddMany<T>(IEnumerable<T> entities, string overrideCollectionName = "") where T : Entity
        {
            if (!entities.Any())
            {
                return;
            }

            var collection = GetCollection<T>(overrideCollectionName);
            await collection.InsertManyAsync(entities, new InsertManyOptions() { IsOrdered = true });
        }

        #endregion

        #region Delete

        public async Task UpsertMany<T>(List<T> entity, string overrideCollectionName = "") where T : Entity
        {
            if (entity.Count == 0)
            {
                return;
            }

            var collection = GetCollection<T>(overrideCollectionName);

            foreach (var x1 in entity.Where(e=>string.IsNullOrWhiteSpace(e.Id)))
            {
                x1.Id = ObjectId.GenerateNewId().ToString();
            }

            var writeModel = entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = true });

            var bulkWriteResult = await collection.BulkWriteAsync(writeModel);

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
            if (entity.Count == 0)
            {
                return;
            }

            var collection = GetCollection<T>(overrideCollectionName);

            var writeModel = entity.Select(f => new ReplaceOneModel<T>(new ExpressionFilterDefinition<T>(e => e.Id == f.Id), f) { IsUpsert = false });

            var bulkWriteResult = await collection.BulkWriteAsync(writeModel);

            if (!bulkWriteResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }
        }

        public async Task Upsert<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);

            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = ObjectId.GenerateNewId().ToString();
            }

            var updateResult = await collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new UpdateOptions { IsUpsert = true });

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpsertException();
            }
        }

        public async Task<bool> Update<T>(T entity, string overrideCollectionName = "") where T : Entity
        {
            var collection = GetCollection<T>(overrideCollectionName);

            var updateResult = await collection.ReplaceOneAsync(e => e.Id == entity.Id, entity, new UpdateOptions() { IsUpsert = false });

            if (!updateResult.IsAcknowledged)
            {
                throw new FailedToUpdateException();
            }

            return true;
        }

        #endregion
    }
}
