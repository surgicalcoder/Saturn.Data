using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IWeakSecondScopedReadonlyRepository
{
    Task<IAsyncEnumerable<TItem>> All<TItem>(string primaryScope, string secondScope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<TItem> ById<TItem>(string primaryScope, string secondScope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem>(string primaryScope, string secondScope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<long> Count<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    IQueryable<TItem> IQueryable<TItem>(string primaryScope, string secondScope)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string primaryScope, string secondScope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<TItem> One<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate, string continueFrom = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem>(string primaryScope, string secondScope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null,
        int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, ISecondScopedById, new();
}

