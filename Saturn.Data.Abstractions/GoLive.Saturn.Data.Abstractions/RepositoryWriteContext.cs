using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public sealed class RepositoryWriteContext<TItem> where TItem : Entity
{
    public RepositoryWriteOperation Operation { get; init; }

    public string Id { get; init; }

    public IReadOnlyCollection<string> Ids { get; init; }

    public IReadOnlyCollection<TItem> Items { get; init; }

    public Expression<Func<TItem, bool>> Filter { get; init; }

    public long? ExpectedVersion { get; init; }

    public string JsonDocument { get; init; }

    public IDataUpdateDefinition<TItem> UpdateDefinition { get; init; }

    public LambdaExpression IncrementField { get; init; }

    public object IncrementDelta { get; init; }

    public IDatabaseTransaction Transaction { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public bool Suppress { get; init; }
}

