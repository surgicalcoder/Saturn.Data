using System.Globalization;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;

namespace Saturn.Data.DocumentDb.ChangeFeed;

public sealed class DocumentDbOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private readonly DocumentDbRepository repository;

    public DocumentDbOutboxChangeFeedSink(DocumentDbRepository repository, string source) : base(source)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var counterId = $"counter:{Source}";
        var counter = await repository.Store.Get<ChangeFeedCounterRow>(counterId).ConfigureAwait(false);
        var next = (counter?.Seq ?? 0) + 1;

        if (counter is null)
        {
            await repository.InsertCounterAsync(transaction, new ChangeFeedCounterRow { Id = counterId, Seq = next }, ct).ConfigureAwait(false);
        }
        else
        {
            counter.Seq = next;
            await repository.UpdateCounterAsync(transaction, counter, ct).ConfigureAwait(false);
        }

        return next;
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);

        var row = new ChangeFeedOutboxRow
        {
            Id = record.Sequence.ToString("D20", CultureInfo.InvariantCulture),
            Seq = record.Sequence,
            Source = record.Source,
            PayloadJson = JsonSerializer.Serialize(record)
        };

        await repository.InsertOutboxRowAsync(transaction, row, ct).ConfigureAwait(false);
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        var rows = await repository.Store.Query<ChangeFeedOutboxRow>()
            .Where(row => row.Source == source && row.Seq > afterSequence)
            .OrderBy(row => row.Seq)
            .Paginate(0, take)
            .ToList(ct)
            .ConfigureAwait(false);

        var result = new List<DataChangeEvent>();

        foreach (var row in rows)
        {
            var record = JsonSerializer.Deserialize<ChangeFeedRecord>(row.PayloadJson);

            if (record is not null)
            {
                result.Add(ChangeFeedRecordMapper.ToEvent(record));
            }
        }

        return result;
    }
}
