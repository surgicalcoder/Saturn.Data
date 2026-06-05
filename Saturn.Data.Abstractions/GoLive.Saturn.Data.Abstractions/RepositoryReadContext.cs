using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public sealed class RepositoryReadContext<TItem> where TItem : Entity
{
    public RepositoryReadOperation Operation { get; init; }

    public bool IncludeDeleted { get; init; }

    public string Id { get; init; }

    public IReadOnlyCollection<string> Ids { get; init; }

    public string ContinueFrom { get; init; }

    public int? PageSize { get; init; }

    public int? PageNumber { get; init; }

    public IReadOnlyCollection<SortOrder<TItem>> SortOrders { get; init; }

    public Expression<Func<TItem, bool>> Predicate { get; init; }

    public IReadOnlyCollection<KeyValuePair<string, object>> WhereClause { get; init; }

    public IDatabaseTransaction Transaction { get; init; }

    public CancellationToken CancellationToken { get; init; }
}

