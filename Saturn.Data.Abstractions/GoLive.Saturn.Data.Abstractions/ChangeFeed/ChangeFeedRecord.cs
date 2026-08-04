using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class ChangeFeedRecord
{
    public string Id { get; set; }

    public long Sequence { get; set; }

    public string Source { get; set; }

    public DateTimeOffset OccuredAtUtc { get; set; }

    public string EntityTypeName { get; set; }

    public string Operation { get; set; }

    public string Outcome { get; set; }

    public List<string> EntityIds { get; set; } = new();

    public bool IsPartial { get; set; }

    public bool HasFullItems { get; set; }

    public string ItemsJson { get; set; }

    public long? Version { get; set; }
}