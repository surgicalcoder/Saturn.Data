using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.Stellar.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableDb>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public UnitTestableDb Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        var baseDirectory = "e:\\_scratch\\_unit_tests\\stellardb_changefeed\\";
        Directory.CreateDirectory(baseDirectory);
        Repository = new UnitTestableDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
        }, new StellarRepositoryOptions()
        {
            BaseDirectory = baseDirectory,
            DatabaseName = $"StellarChangeFeed_{DateTime.UtcNow.ToString("O").Replace(":", "_").Replace(".", "_").Replace("-", "_")}"
        });
    }

    public void Dispose()
    {
    }
}
