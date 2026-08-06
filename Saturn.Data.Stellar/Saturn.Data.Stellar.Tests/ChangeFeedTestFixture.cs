using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Saturn.Data.Stellar.ChangeFeed;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Stellar.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableDb>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public IChangeFeedSink Sink { get; }

    public bool SupportsTransactions => false;

    public IList<IRepositoryWriteBehavior> WriteBehaviors { get; private set; }

    public UnitTestableDb Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        var baseDirectory = "e:\\_scratch\\_unit_tests\\stellardb_changefeed\\";
        Directory.CreateDirectory(baseDirectory);
        var stellarOptions = new StellarRepositoryOptions()
        {
            BaseDirectory = baseDirectory,
            DatabaseName = $"StellarChangeFeed_{DateTime.UtcNow.ToString("O").Replace(":", "_").Replace(".", "_").Replace("-", "_")}"
        };

        Repository = new UnitTestableDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
        }, stellarOptions);

        Sink = new StellarOutboxChangeFeedSink(Repository.Database, "test-source");
        Repository.Options.WriteBehaviors.Add(
            new ChangeFeedBehavior(Sink, "test-source", new ChangeFeedBehaviorOptions { Enabled = true }));
        WriteBehaviors = Repository.Options.WriteBehaviors;
    }

    public void Dispose()
    {
    }
}