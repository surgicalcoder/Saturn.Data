namespace Saturn.Data.Testing.Shared.ChangeFeed;

public interface IChangeFeedTestFixture
{
    RecordingWriteBehavior Recorder { get; }
}
