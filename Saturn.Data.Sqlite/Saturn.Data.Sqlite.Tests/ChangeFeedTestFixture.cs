using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.Sqlite.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Sqlite.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableSqliteRepository>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => true;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; }

    public UnitTestableSqliteRepository Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();

        var path = Path.Combine(Path.GetTempPath(), $"saturn-sqlite-changefeed-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableSqliteRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name,
                WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
            },
            new SqliteRepositoryOptions { DataSource = path });

        Repository.DropRecreateDatabase();

        Sink = new SqliteOutboxChangeFeedSink(Repository, "test-source");
        Repository.Options.WriteBehaviors.Add(new ChangeFeedBehavior(Sink, "test-source", new ChangeFeedBehaviorOptions { Enabled = true }));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose() => Repository.Dispose();
}
