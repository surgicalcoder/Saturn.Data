using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using MongoDB.Driver;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.MongoDb.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableMongoDbRepository>, IChangeFeedTestFixture
{
    private static readonly bool transactionsSupported = ProbeTransactionsSupported();

    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => transactionsSupported;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; private set; }

    public UnitTestableMongoDbRepository Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        var mongoOptions = new MongoDbRepositoryOptions
        {
            ConnectionString = "mongodb://localhost:27017/UnitTests"
        };

        var database = new MongoClient("mongodb://localhost:27017/UnitTests").GetDatabase("UnitTests");
        Sink = new MongoOutboxChangeFeedSink(database, "test-source");

        var behaviors = new List<IRepositoryWriteBehavior>
        {
            Recorder,
            new ChangeFeedBehavior(Sink, "test-source")
        };

        WriteBehaviors = behaviors;

        Repository = new UnitTestableMongoDbRepository(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = behaviors
        }, mongoOptions);
    }

    public void Dispose()
    {
    }

    private static bool ProbeTransactionsSupported()
    {
        try
        {
            var client = new MongoClient("mongodb://localhost:27017/UnitTests");
            using var session = client.StartSession();
            session.StartTransaction();
            session.AbortTransaction();
            return true;
        }
        catch
        {
            return false;
        }
    }
}