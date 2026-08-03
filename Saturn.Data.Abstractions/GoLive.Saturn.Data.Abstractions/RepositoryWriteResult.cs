using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Abstractions;

public sealed class RepositoryWriteResult
{
    public RepositoryWriteOperation Operation { get; init; }

    public bool Succeeded { get; init; }

    public bool PartialFailure { get; init; }

    public WriteOutcome Outcome { get; init; }

    public int AffectedCount { get; init; }

    public int FailedCount { get; init; }

    public IReadOnlyCollection<string> EntityIds { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> FailedIds { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> MatchedIds { get; init; } = Array.Empty<string>();

    public bool WasCreated { get; init; }

    public object RawResult { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }
}
