using System.Globalization;
using System.Text.Json;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.ChangeFeed;

public sealed class SqliteOutboxChangeFeedSink : OutboxChangeFeedSink
{
    private readonly SqliteRepository repository;

    public SqliteOutboxChangeFeedSink(SqliteRepository repository, string source) : base(source)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async ValueTask<long> NextSequenceAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        await using var lease = await repository.RentConnectionInternalAsync(transaction, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "__change_feed_counters"(source, seq)
            VALUES (@source, 1)
            ON CONFLICT(source) DO UPDATE SET seq = seq + 1
            RETURNING seq;
            """;
        command.Parameters.AddWithValue("@source", Source);

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }

    protected override async ValueTask AppendRowAsync(DataChangeEvent change, IDatabaseTransaction transaction, CancellationToken ct)
    {
        var record = ChangeFeedRecordMapper.ToRecord(change);

        await using var lease = await repository.RentConnectionInternalAsync(transaction, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "__change_feed"
                (seq, change_id, source, entity_type, occurred_utc, operation, outcome, entity_ids, is_partial, has_full_items, items_json, version)
            VALUES
                (@seq, @changeId, @source, @entityType, @occurredUtc, @operation, @outcome, @entityIds, @isPartial, @hasFullItems, @itemsJson, @version);
            """;
        command.Parameters.AddWithValue("@seq", record.Sequence);
        command.Parameters.AddWithValue("@changeId", record.Id);
        command.Parameters.AddWithValue("@source", record.Source);
        command.Parameters.AddWithValue("@entityType", record.EntityTypeName);
        command.Parameters.AddWithValue("@occurredUtc", record.OccuredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@operation", record.Operation);
        command.Parameters.AddWithValue("@outcome", record.Outcome);
        command.Parameters.AddWithValue("@entityIds", JsonSerializer.Serialize(record.EntityIds));
        command.Parameters.AddWithValue("@isPartial", record.IsPartial ? 1 : 0);
        command.Parameters.AddWithValue("@hasFullItems", record.HasFullItems ? 1 : 0);
        command.Parameters.AddWithValue("@itemsJson", (object?)record.ItemsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@version", (object?)record.Version ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    protected override async ValueTask<IReadOnlyList<DataChangeEvent>> ReadRowsAsync(string source, long afterSequence, int take, CancellationToken ct)
    {
        await using var lease = await repository.RentConnectionInternalAsync(null, ct).ConfigureAwait(false);
        await EnsureTablesAsync(lease.Connection, ct).ConfigureAwait(false);

        await using var command = lease.Connection.CreateCommand();
        command.CommandText = """
            SELECT seq, change_id, source, entity_type, occurred_utc, operation, outcome, entity_ids, is_partial, has_full_items, items_json, version
            FROM "__change_feed"
            WHERE source = @source AND seq > @after
            ORDER BY seq
            LIMIT @take;
            """;
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@after", afterSequence);
        command.Parameters.AddWithValue("@take", take);

        var result = new List<DataChangeEvent>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var record = new ChangeFeedRecord
            {
                Sequence = reader.GetInt64(0),
                Id = reader.GetString(1),
                Source = reader.GetString(2),
                EntityTypeName = reader.GetString(3),
                OccuredAtUtc = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                Operation = reader.GetString(5),
                Outcome = reader.GetString(6),
                EntityIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(7)) ?? new List<string>(),
                IsPartial = reader.GetInt32(8) == 1,
                HasFullItems = reader.GetInt32(9) == 1,
                ItemsJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                Version = reader.IsDBNull(11) ? null : reader.GetInt64(11)
            };

            result.Add(ChangeFeedRecordMapper.ToEvent(record));
        }

        return result;
    }

    private static async Task EnsureTablesAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__change_feed" (
                seq INTEGER NOT NULL PRIMARY KEY,
                change_id TEXT NOT NULL,
                source TEXT NOT NULL,
                entity_type TEXT NOT NULL,
                occurred_utc TEXT NOT NULL,
                operation TEXT NOT NULL,
                outcome TEXT NOT NULL,
                entity_ids TEXT NOT NULL,
                is_partial INTEGER NOT NULL,
                has_full_items INTEGER NOT NULL,
                items_json TEXT NULL,
                version INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS "ix___change_feed_source_seq" ON "__change_feed"(source, seq);
            CREATE TABLE IF NOT EXISTS "__change_feed_counters" (
                source TEXT NOT NULL PRIMARY KEY,
                seq INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
