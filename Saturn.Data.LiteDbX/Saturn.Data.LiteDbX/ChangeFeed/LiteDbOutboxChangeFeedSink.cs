using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using LiteDbX;
using LiteDbX.Engine;

namespace Saturn.Data.LiteDbX.ChangeFeed;

public class LiteDbOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private const string FeedCollection = "__change_feed";
    private const string CounterCollection = "__change_feed_counters";

    private readonly LiteDatabase database;

    public LiteDbOutboxChangeFeedSink(LiteDatabase database, string source)
        : base(source)
    {
        this.database = database ?? throw new System.ArgumentNullException(nameof(database));
    }

    private static ILiteTransaction ResolveLiteTransaction(IDatabaseTransaction transaction)
    {
        return transaction is LiteDbXTransactionWrapper wrapper ? wrapper.Inner : null;
    }

    private ILiteCollection<ChangeFeedRecord> Feed => database.GetCollection<ChangeFeedRecord>(FeedCollection);

    private ILiteCollection<ChangeFeedCounter> Counters => database.GetCollection<ChangeFeedCounter>(CounterCollection);

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var counterId = $"{change.Source}|{change.EntityType.FullName}";
        var liteTx = ResolveLiteTransaction(transaction);
        var counter = liteTx != null
            ? await Counters.AsQueryable(liteTx).Where(c => c.Id == counterId).FirstOrDefaultAsync(ct)
            : await Counters.FindOne(c => c.Id == counterId);

        long sequence;
        if (counter == null)
        {
            sequence = 1;
            await Counters.Insert(new ChangeFeedCounter { Id = counterId, Sequence = sequence }, liteTx, ct);
        }
        else
        {
            sequence = counter.Sequence + 1;
            counter.Sequence = sequence;
            await Counters.Update(counter, ct);
        }

        return sequence;
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);
        await Feed.Insert(record, ResolveLiteTransaction(transaction), ct);
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        var records = await Feed.AsQueryable()
            .Where(r => r.Source == source && r.Sequence > afterSequence)
            .OrderBy(r => r.Sequence)
            .Take(take)
            .ToListAsync(ct);

        return records.Select(ChangeFeedRecordMapper.ToEvent).ToList();
    }
}