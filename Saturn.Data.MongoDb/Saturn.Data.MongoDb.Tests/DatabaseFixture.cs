using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;

namespace Saturn.Data.MongoDb.Tests;

public class DatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableMongoDbRepository>
{
    public UnitTestableMongoDbRepository Repository { get; }

    public DatabaseFixture()
    {
        Repository = new UnitTestableMongoDbRepository(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name
        }, new MongoDbRepositoryOptions()
        {
            ConnectionString = "mongodb://localhost:27017/UnitTests",
        });
        
        // Initialize database once
        Repository.DropRecreateDatabase();
    }

    public void Dispose()
    {
        Repository.DropRecreateDatabase();
    }
}
