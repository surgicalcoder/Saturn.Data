using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public interface IChangeFeed<TItem>
    where TItem : Entity
{
    ValueTask SubscribeAsync(Func<DataChangeEvent<TItem>, CancellationToken, ValueTask> handler,
        long? afterSequence = null, CancellationToken ct = default);

    ValueTask<IReadOnlyList<DataChangeEvent<TItem>>> ReadAsync(long afterSequence, int take, CancellationToken ct = default);
}
