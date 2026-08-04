using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class ChangeFeedPoller
{
    private const int DefaultBatchSize = 128;

    private readonly IChangeFeedSink sink;
    private readonly ConcurrentDictionary<(string Source, Type EntityType), Subscription> subscriptions = new();

    public ChangeFeedPoller(IChangeFeedSink sink)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public IChangeFeed<TItem> For<TItem>(string source)
        where TItem : Entity
        => new TypedFeed<TItem>(this, source);

    public async ValueTask<int> DrainAsync(CancellationToken ct = default)
    {
        var delivered = 0;
        foreach (var pair in subscriptions.ToArray())
        {
            delivered += await DrainAsync(pair.Value, ct);
        }
        return delivered;
    }

    private async ValueTask<int> DrainAsync(Subscription subscription, CancellationToken ct)
    {
        var delivered = 0;
        while (true)
        {
            var events = await sink.ReadAsync(subscription.Source, subscription.Watermark, subscription.BatchSize, ct);
            if (events.Count == 0)
            {
                break;
            }

            var matching = events.Where(e => e.EntityType == subscription.EntityType).ToList();
            foreach (var change in matching)
            {
                await subscription.Handler(change, ct);
                delivered++;
            }

            subscription.Watermark = Math.Max(subscription.Watermark, events.Max(e => e.Sequence));

            if (events.Count < subscription.BatchSize)
            {
                break;
            }
        }

        return delivered;
    }

    private async ValueTask<IReadOnlyList<DataChangeEvent<TItem>>> ReadAsync<TItem>(string source, long afterSequence, int take, CancellationToken ct)
        where TItem : Entity
    {
        var events = await sink.ReadAsync(source, afterSequence, take, ct);
        return events.Where(e => e.EntityType == typeof(TItem)).Select(AsTyped<TItem>).ToList();
    }

    private void Subscribe<TItem>(string source, Func<DataChangeEvent<TItem>, CancellationToken, ValueTask> handler, long? afterSequence)
        where TItem : Entity
    {
        var subscription = new Subscription
        {
            Source = source,
            EntityType = typeof(TItem),
            Watermark = afterSequence ?? 0,
            Handler = (change, token) => handler(AsTyped<TItem>(change), token)
        };
        subscriptions[(source, typeof(TItem))] = subscription;
    }

    private static DataChangeEvent<TItem> AsTyped<TItem>(DataChangeEvent change)
        where TItem : Entity
        => new DataChangeEvent<TItem>
        {
            ChangeId = change.ChangeId,
            Sequence = change.Sequence,
            Source = change.Source,
            OccuredAtUtc = change.OccuredAtUtc,
            EntityType = change.EntityType,
            Operation = change.Operation,
            Outcome = change.Outcome,
            EntityIds = change.EntityIds,
            IsPartial = change.IsPartial,
            HasFullItems = change.HasFullItems,
            Items = change.Items,
            Version = change.Version
        };

    private sealed class Subscription
    {
        public string Source { get; init; }

        public Type EntityType { get; init; }

        public long Watermark { get; set; }

        public int BatchSize { get; init; } = DefaultBatchSize;

        public Func<DataChangeEvent, CancellationToken, ValueTask> Handler { get; init; }
    }

    private sealed class TypedFeed<TItem> : IChangeFeed<TItem>
        where TItem : Entity
    {
        private readonly ChangeFeedPoller poller;
        private readonly string source;

        public TypedFeed(ChangeFeedPoller poller, string source)
        {
            this.poller = poller;
            this.source = source;
        }

        public ValueTask SubscribeAsync(Func<DataChangeEvent<TItem>, CancellationToken, ValueTask> handler, long? afterSequence = null, CancellationToken ct = default)
        {
            poller.Subscribe(source, handler, afterSequence);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<DataChangeEvent<TItem>>> ReadAsync(long afterSequence, int take, CancellationToken ct = default)
            => poller.ReadAsync<TItem>(source, afterSequence, take, ct);
    }
}
