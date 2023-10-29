using System;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public class SortOrder<T> where T :Entity
{
    public SortDirection Direction { get; set; }
    public Func<T, object> Field { get; set; }
}