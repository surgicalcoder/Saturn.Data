using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IRepositoryReadBehavior
{
    Expression<Func<TItem, bool>> BeforeQueryExecution<TItem>(Expression<Func<TItem, bool>> predicate, RepositoryReadContext<TItem> context)
        where TItem : Entity
        => predicate;

    IQueryable<TItem> BeforeQueryExecution<TItem>(IQueryable<TItem> query, RepositoryReadContext<TItem> context)
        where TItem : Entity
        => query;

    ValueTask AfterMaterialization<TItem>(IReadOnlyCollection<TItem> items, RepositoryReadContext<TItem> context)
        where TItem : Entity
        => ValueTask.CompletedTask;
}

