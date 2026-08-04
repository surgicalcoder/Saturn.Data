using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public static class ChangeFeedRecordMapper
{
    public static DataChangeEvent ToEvent(ChangeFeedRecord record)
    {
        var entityType = Type.GetType(record.EntityTypeName, throwOnError: false);

        var items = new List<object>();
        if (!string.IsNullOrEmpty(record.ItemsJson))
        {
            items = JsonSerializer.Deserialize<List<object>>(record.ItemsJson) ?? new List<object>();
        }

        return new DataChangeEvent
        {
            ChangeId = record.Id,
            Sequence = record.Sequence,
            Source = record.Source,
            OccuredAtUtc = record.OccuredAtUtc,
            EntityType = entityType,
            Operation = Enum.Parse<RepositoryWriteOperation>(record.Operation),
            Outcome = Enum.Parse<WriteOutcome>(record.Outcome),
            EntityIds = record.EntityIds,
            IsPartial = record.IsPartial,
            HasFullItems = record.HasFullItems,
            Items = items,
            Version = record.Version
        };
    }

    public static ChangeFeedRecord ToRecord(DataChangeEvent change)
    {
        var itemsJson = change.HasFullItems
            ? JsonSerializer.Serialize(change.Items)
            : null;

        return new ChangeFeedRecord
        {
            Id = change.ChangeId,
            Sequence = change.Sequence,
            Source = change.Source,
            OccuredAtUtc = change.OccuredAtUtc,
            EntityTypeName = change.EntityType.FullName,
            Operation = change.Operation.ToString(),
            Outcome = change.Outcome.ToString(),
            EntityIds = change.EntityIds.ToList(),
            IsPartial = change.IsPartial,
            HasFullItems = change.HasFullItems,
            ItemsJson = itemsJson,
            Version = change.Version
        };
    }
}