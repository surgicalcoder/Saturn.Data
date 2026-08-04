using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public abstract class OutboxChangeFeedSink : IChangeFeedSink
{
    protected OutboxChangeFeedSink(string source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    protected string Source { get; }

    public async ValueTask AppendAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var sequence = await NextSequenceAsync(change, transaction, ct);
        var stamped = change with { Sequence = sequence };
        await AppendRowAsync(stamped, transaction, ct);
    }

    public async ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(string source, long afterSequence, int take, CancellationToken ct)
        => await ReadRowsAsync(source, afterSequence, take, ct);

    protected abstract ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct);

    protected abstract ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct);

    protected abstract ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct);
}