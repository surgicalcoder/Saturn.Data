using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace GoLive.Saturn.Data.Entities;

public partial class Ref<T>
{
    public void Fetch(IQueryable<T> items)
    {
        Item = items.FirstOrDefault(f => f.Id == _refId);
    }

    public void Fetch(IList<T> items)
    {
        Item = items.FirstOrDefault(f => f.Id == _refId);
    }

    public void Fetch(Func<string, T> expr)
    {
        this.Item = expr.Invoke(this.Id);
    }
}