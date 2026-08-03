using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.MongoDb.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableMongoDbRepository>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public UnitTestableMongoDbRepository Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        Repository = new UnitTestableMongoDbRepository(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
        }, new MongoDbRepositoryOptions()
        {
            ConnectionString = "mongodb://localhost:27017/UnitTests"
        });
    }

    public void Dispose()
    {
    }
}
