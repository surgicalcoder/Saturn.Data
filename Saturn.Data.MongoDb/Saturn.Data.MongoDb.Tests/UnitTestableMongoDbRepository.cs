using GoLive.Saturn.Data.Abstractions;

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
};