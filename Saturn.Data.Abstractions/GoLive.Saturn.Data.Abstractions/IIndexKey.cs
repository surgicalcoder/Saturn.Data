using System;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IIndexKey<TItem> where TItem : Entity
{
    Expression<Func<TItem, object>> Field { get; }

    IndexSortDirection Direction { get; }
}

