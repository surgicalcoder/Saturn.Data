using System;
using System.Collections.Generic;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public record DataChangeEvent
{
    public string ChangeId { get; init; }

    public long Sequence { get; init; }

    public string Source { get; init; }

    public DateTimeOffset OccuredAtUtc { get; init; }

    public Type EntityType { get; init; }

    public RepositoryWriteOperation Operation { get; init; }

    public WriteOutcome Outcome { get; init; }

    public IReadOnlyCollection<string> EntityIds { get; init; } = Array.Empty<string>();

    public bool IsPartial { get; init; }

    public bool HasFullItems { get; init; }

    public IReadOnlyCollection<object> Items { get; init; } = Array.Empty<object>();

    public long? Version { get; init; }
}

public record DataChangeEvent<TItem> : DataChangeEvent
    where TItem : Entity
{
    public IReadOnlyCollection<TItem> TypedItems { get; init; } = Array.Empty<TItem>();
}