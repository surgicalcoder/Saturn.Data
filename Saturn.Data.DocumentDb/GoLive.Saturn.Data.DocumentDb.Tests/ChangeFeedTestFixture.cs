using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.DocumentDb.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Shiny.DocumentDb.Sqlite;

namespace Saturn.Data.DocumentDb.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => Repository.Capabilities.SupportsTransactions;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; }

    public UnitTestableDocumentDbRepository Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();

        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-changefeed-{Guid.NewGuid():N}.db");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name,
                WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
            },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(SqliteDatabaseProvider),
                DatabaseProvider = new SqliteDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();

        Sink = new DocumentDbOutboxChangeFeedSink(Repository, "test-source");
        Repository.Options.WriteBehaviors.Add(new ChangeFeedBehavior(Sink, "test-source", new ChangeFeedBehaviorOptions { Enabled = true }));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose() => Repository.Dispose();
}
