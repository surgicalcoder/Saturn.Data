using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public interface IReadonlyRepository : IDisposable
{
        
    Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    IQueryable<TItem> IQueryable<TItem>() where TItem : Entity;
    
    Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = null, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
    
    Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;

    Task<TItem> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) 
        where TItem : Entity;
}