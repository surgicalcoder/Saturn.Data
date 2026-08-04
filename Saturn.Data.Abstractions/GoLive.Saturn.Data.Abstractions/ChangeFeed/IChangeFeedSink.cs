using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public interface IChangeFeedSink
{
    ValueTask AppendAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct);

    ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(string source, long afterSequence, int take, CancellationToken ct);
}