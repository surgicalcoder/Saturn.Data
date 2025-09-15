using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using LiteDB.Queryable;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await GetCollection<TItem>().FindByIdAsync(id);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var results = await GetCollection<TItem>().FindAsync(entity => IDs.Contains(entity.Id));

        return results.ToAsyncEnumerable();
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return Task.FromResult(GetCollection<TItem>().AsQueryable().ToAsyncEnumerable());
    }

    public virtual IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var finalQuery = BuildQuery(collection, predicate, continueFrom);
        finalQuery = ApplySortOrders(finalQuery, sortOrders);

        var results = await finalQuery.ToListAsync();
        return results.ToAsyncEnumerable();
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var parameter = Expression.Parameter(typeof(TItem), "entity");
            var property = Expression.Property(parameter, clause.Key);
            var constant = Expression.Constant(clause.Value);
            var equal = Expression.Equal(property, constant);
            var lambda = Expression.Lambda<Func<TItem, bool>>(equal, parameter);
            query = query.Where(lambda);
        }

        query = ApplyContinueFrom(query, continueFrom);
        query = ApplySortOrders(query, sortOrders);
        query = ApplyPagination(query, pageSize);
    
        return query.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var finalQuery = BuildQuery(collection, predicate, continueFrom);
        finalQuery = ApplySortOrders(finalQuery, sortOrders);

        return await finalQuery.FirstOrDefaultAsync();
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var query = collection.Query();
        
        if (predicate != null)
        {
            var predExpr = BsonMapper.Global.GetExpression(predicate);
            query = query.Where(predExpr);
        }
        
        var items = query.OrderBy(x => Guid.NewGuid()).Limit(count);

        var results = await items.ToListAsync();
        return results.ToAsyncEnumerable();
    }
    
    public virtual async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await GetCollection<TItem>().LongCountAsync(TransformRefEntityComparisons(predicate));
    }
}
