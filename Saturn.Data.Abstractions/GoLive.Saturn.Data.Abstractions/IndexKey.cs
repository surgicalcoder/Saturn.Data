using System;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public sealed class IndexKey<TItem> : IIndexKey<TItem> where TItem : Entity
{
    public IndexKey(Expression<Func<TItem, object>> field, IndexSortDirection direction = IndexSortDirection.Ascending)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Direction = direction;
    }

    public Expression<Func<TItem, object>> Field { get; }

    public IndexSortDirection Direction { get; }
}

