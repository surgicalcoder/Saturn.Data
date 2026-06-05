using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public class SortOrderBuilder<T> where T : Entity
{
    private readonly List<SortOrder<T>> order = new();

    public SortOrderBuilder() { }

    public SortOrderBuilder<T> Ascending(Expression<Func<T, object>> field)
    {
        return OrderBy(field);
    }

    public SortOrderBuilder<T> OrderBy(Expression<Func<T, object>> field)
    {
        order.Add(new SortOrder<T>(field, SortDirection.Ascending));
        return this;
    }

    public List<SortOrder<T>> Build() => order;

    public SortOrderBuilder<T> Descending(Expression<Func<T, object>> field)
    {
        return OrderByDescending(field);
    }

    public SortOrderBuilder<T> OrderByDescending(Expression<Func<T, object>> field)
    {
        order.Add(new SortOrder<T>(field, SortDirection.Descending));
        return this;
    }

    public SortOrderBuilder<T> ThenBy(Expression<Func<T, object>> field)
    {
        return OrderBy(field);
    }

    public SortOrderBuilder<T> ThenByDescending(Expression<Func<T, object>> field)
    {
        return OrderByDescending(field);
    }

    public static implicit operator List<SortOrder<T>>(SortOrderBuilder<T> builder)
    {
        return builder.order;
    }

    // For fluent static usage: SortOrderBuilder<T>.StartAscending(...).Descending(...)
    public static SortOrderBuilder<T> StartAscending(Expression<Func<T, object>> field)
    {
        return new SortOrderBuilder<T>().OrderBy(field);
    }

    public static SortOrderBuilder<T> StartDescending(Expression<Func<T, object>> field)
    {
        return new SortOrderBuilder<T>().OrderByDescending(field);
    }
}