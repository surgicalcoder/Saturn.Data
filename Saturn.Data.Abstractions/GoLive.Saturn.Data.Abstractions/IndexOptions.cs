using System;

namespace GoLive.Saturn.Data.Abstractions;

public sealed class IndexOptions
{
    public bool Unique { get; init; }

    public bool Sparse { get; init; }

    public bool Background { get; init; } = true;

    public bool HasExpireAfter { get; init; }

    public TimeSpan ExpireAfter { get; init; }
}


