using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;

namespace GoLive.Saturn.Data.Benchmarks
{
    public class TestMongoClient : IMongoClient
    {
        public void DropDatabase(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public void DropDatabase(IClientSessionHandle session, string name, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task DropDatabaseAsync(string name, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task DropDatabaseAsync(IClientSessionHandle session, string name, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public IMongoDatabase GetDatabase(string name, MongoDatabaseSettings settings = null) => new TestMongoDatabase();

        public IAsyncCursor<string> ListDatabaseNames(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public IAsyncCursor<string> ListDatabaseNames(IClientSessionHandle session, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task<IAsyncCursor<string>> ListDatabaseNamesAsync(IClientSessionHandle session,
            CancellationToken cancellationToken = new CancellationToken()) =>
            throw new NotImplementedException();

        public IAsyncCursor<BsonDocument> ListDatabases(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public IAsyncCursor<BsonDocument> ListDatabases(ListDatabasesOptions options, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public IAsyncCursor<BsonDocument> ListDatabases(IClientSessionHandle session, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public IAsyncCursor<BsonDocument> ListDatabases(IClientSessionHandle session, ListDatabasesOptions options,
            CancellationToken cancellationToken = new CancellationToken()) =>
            throw new NotImplementedException();

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(ListDatabasesOptions options, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(IClientSessionHandle session, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<BsonDocument>> ListDatabasesAsync(IClientSessionHandle session, ListDatabasesOptions options,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public IClientSessionHandle StartSession(ClientSessionOptions options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<IClientSessionHandle> StartSessionAsync(ClientSessionOptions options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public IAsyncCursor<TResult> Watch<TResult>(PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public IAsyncCursor<TResult> Watch<TResult>(IClientSessionHandle session, PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline,
            ChangeStreamOptions options = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<TResult>> WatchAsync<TResult>(PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncCursor<TResult>> WatchAsync<TResult>(IClientSessionHandle session, PipelineDefinition<ChangeStreamDocument<BsonDocument>, TResult> pipeline, ChangeStreamOptions options = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public IMongoClient WithReadConcern(ReadConcern readConcern) => this;

        public IMongoClient WithReadPreference(ReadPreference readPreference) => this;

        public IMongoClient WithWriteConcern(WriteConcern writeConcern) => this;

        public ICluster Cluster { get; }
        public MongoClientSettings Settings { get; }
    }
}