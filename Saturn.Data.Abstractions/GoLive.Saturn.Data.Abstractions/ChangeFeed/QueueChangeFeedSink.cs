using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class QueueChangeFeedSink : IChangeFeedSink
{
    private readonly IChangeFeedPublisher publisher;

    public QueueChangeFeedSink(IChangeFeedPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public ValueTask AppendAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
        => publisher.PublishAsync(change, ct);

    public ValueTask<IReadOnlyList<DataChangeEvent>> ReadAsync(
        string source, long afterSequence, int take, CancellationToken ct)
        => new ValueTask<IReadOnlyList<DataChangeEvent>>(Array.Empty<DataChangeEvent>());
}
