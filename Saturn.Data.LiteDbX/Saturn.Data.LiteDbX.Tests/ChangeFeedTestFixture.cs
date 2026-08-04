using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.LiteDbX.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.LiteDbX.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableLiteDb>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => true;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; private set; }

    public UnitTestableLiteDb Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        var liteDbOptions = new LiteDBRepositoryOptions
        {
            ConnectionString = "Filename=\"e:\\_scratch\\litedb-changefeed-tests.db\";Connection=Direct"
        };

        Repository = new UnitTestableLiteDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
        }, liteDbOptions);

        Sink = new LiteDbOutboxChangeFeedSink(Repository.Database, "test-source");
        Repository.Options.WriteBehaviors.Add(new ChangeFeedBehavior(Sink, "test-source"));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose()
    {
    }
}