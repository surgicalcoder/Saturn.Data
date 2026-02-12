﻿using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb.Tests;

public class UnitTestableMongoDbRepository(RepositoryOptions repositoryOptions, MongoDbRepositoryOptions mongoDbRepositoryOptions)
    : MongoDbRepository(repositoryOptions, mongoDbRepositoryOptions)
{
    public void DropRecreateDatabase()
    {
        var databaseName = mongoDatabase.DatabaseNamespace.DatabaseName;
        client.DropDatabase(databaseName);
        _ = client.GetDatabase(databaseName);
    }

    /// <summary>
    /// Gets the raw BSON collection for testing purposes (to inspect serialization)
    /// </summary>
    public IMongoCollection<BsonDocument> GetRawCollection<T>() where T : Entity
    {
        return mongoDatabase.GetCollection<BsonDocument>(GetCollectionNameForType<T>());
    }
};