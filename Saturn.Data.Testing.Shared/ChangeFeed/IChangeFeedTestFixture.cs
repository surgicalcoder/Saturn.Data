using System.Collections.Generic;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;

namespace Saturn.Data.Testing.Shared.ChangeFeed;

public interface IChangeFeedTestFixture
{
    RecordingWriteBehavior Recorder { get; }

    IChangeFeedSink Sink { get; }

    bool SupportsTransactions { get; }

    IList<IRepositoryWriteBehavior> WriteBehaviors { get; }
}