using GoLive.Saturn.Data.Abstractions;
using Saturn.Data.Testing.Shared;
using Saturn.Data.Testing.Shared.ChangeFeed;

namespace Saturn.Data.LiteDbX.Tests;

public class ChangeFeedTestFixture : IDisposable, IRepositoryTestFixture<UnitTestableLiteDb>, IChangeFeedTestFixture
{
    public RecordingWriteBehavior Recorder { get; }

    public UnitTestableLiteDb Repository { get; }

    public ChangeFeedTestFixture()
    {
        Recorder = new RecordingWriteBehavior();
        Repository = new UnitTestableLiteDb(new RepositoryOptions()
        {
            GetCollectionName = type => type.Name,
            WriteBehaviors = new List<IRepositoryWriteBehavior> { Recorder }
        }, new LiteDBRepositoryOptions()
        {
            ConnectionString = "Filename=\"e:\\_scratch\\litedb-changefeed-tests.db\";Connection=LockFile"
        });
    }

    public void Dispose()
    {
    }
}
