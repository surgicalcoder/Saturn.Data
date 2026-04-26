using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IWeakScopedReadonlyRepository : IDisposable
{
    Task<IAsyncEnumerable<TItem>> All<TItem>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<TItem> ById<TItem>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<IAsyncEnumerable<TItem>> ById<TItem>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<long> Count<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    IQueryable<TItem> IQueryable<TItem>(string scope)
        where TItem : Entity, IScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<IAsyncEnumerable<TItem>> Many<TItem>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<TItem> One<TItem>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();

    Task<IAsyncEnumerable<TItem>> Random<TItem>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity, IScopedById, new();
}

