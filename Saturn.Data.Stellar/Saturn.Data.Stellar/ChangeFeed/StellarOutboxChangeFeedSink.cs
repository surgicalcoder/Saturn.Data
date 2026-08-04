using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Stellar.Collections;

namespace Saturn.Data.Stellar.ChangeFeed;

public class StellarOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private const string FeedCollection = "__change_feed";
    private const string CounterCollection = "__change_feed_counters";

    private readonly FastDB database;

    public StellarOutboxChangeFeedSink(FastDB database, string source)
        : base(source)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    private async Task<IFastDBCollection<EntityId, ChangeFeedRecord>> FeedAsync()
        => await database.GetCollectionAsync<EntityId, ChangeFeedRecord>(FeedCollection);

    private async Task<IFastDBCollection<EntityId, ChangeFeedCounter>> CountersAsync()
        => await database.GetCollectionAsync<EntityId, ChangeFeedCounter>(CounterCollection);

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var counterId = $"{change.Source}|{change.EntityType.FullName}";
        var key = KeyFor(counterId);
        var counters = await CountersAsync();

        long sequence;
        if (counters.TryGet(key, out var counter))
        {
            sequence = counter.Sequence + 1;
            counter.Sequence = sequence;
            await counters.UpdateAsync(key, counter);
        }
        else
        {
            sequence = 1;
            await counters.AddAsync(key, new ChangeFeedCounter { Id = counterId, Sequence = sequence });
        }

        return sequence;
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);
        var feed = await FeedAsync();
        await feed.AddAsync(KeyFor(record.Id), record);
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        var feed = await FeedAsync();
        var records = feed.AsEnumerable()
            .Select(kvp => kvp.Value)
            .Where(r => r.Source == source && r.Sequence > afterSequence)
            .OrderBy(r => r.Sequence)
            .Take(take)
            .ToList();

        return records.Select(ChangeFeedRecordMapper.ToEvent).ToList();
    }

    private static EntityId KeyFor(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var idBytes = new byte[12];
        Array.Copy(bytes, idBytes, 12);
        return new EntityId(idBytes);
    }
}