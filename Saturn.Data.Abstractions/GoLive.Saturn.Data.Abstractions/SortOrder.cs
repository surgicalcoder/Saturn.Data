using System;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public class SortOrder<T> where T :Entity
{
    public SortOrder() { }
    public SortOrder(Expression<Func<T, object>> field, SortDirection direction)
    {
        Direction = direction;
        Field = field;
    }
    public SortDirection Direction { get; set; }
    public Expression<Func<T, object>> Field { get; set; }
}