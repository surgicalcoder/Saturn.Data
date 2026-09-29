using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.DocumentDb.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.Cascade;
using Saturn.Data.Testing.Shared.ChangeFeed;
using Shiny.DocumentDb.DuckDb;

namespace Saturn.Data.DocumentDb.Tests;

[CollectionDefinition("DuckDb", DisableParallelization = true)]
public sealed class DuckDbCollection
{
}

public class DuckDbDatabaseFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public DuckDbDatabaseFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-{Guid.NewGuid():N}.duckdb");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(DuckDbDatabaseProvider),
                DatabaseProvider = new DuckDbDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}

public class DuckDbCascadeTestFixture : IDisposable, ICascadeTestFixture<UnitTestableDocumentDbRepository>
{
    public UnitTestableDocumentDbRepository Repository { get; }

    public DuckDbCascadeTestFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-cascade-{Guid.NewGuid():N}.duckdb");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions { GetCollectionName = type => type.Name },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(DuckDbDatabaseProvider),
                DatabaseProvider = new DuckDbDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose() => Repository.Dispose();
}

public class DuckDbChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableDocumentDbRepository>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => Repository.Capabilities.SupportsTransactions;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; }

    public UnitTestableDocumentDbRepository Repository { get; }

    public DuckDbChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();

        var path = Path.Combine(Path.GetTempPath(), $"saturn-docdb-changefeed-{Guid.NewGuid():N}.duckdb");

        Repository = new UnitTestableDocumentDbRepository(
            new RepositoryOptions
            {
                GetCollectionName = type => type.Name,
                WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
            },
            new DocumentDbRepositoryOptions
            {
                BackendName = nameof(DuckDbDatabaseProvider),
                DatabaseProvider = new DuckDbDatabaseProvider($"Data Source={path}")
            });

        Repository.InitializeAsync().GetAwaiter().GetResult();

        Sink = new DocumentDbOutboxChangeFeedSink(Repository, "test-source");
        Repository.Options.WriteBehaviors.Add(new ChangeFeedBehavior(Sink, "test-source", new ChangeFeedBehaviorOptions { Enabled = true }));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose() => Repository.Dispose();
}
