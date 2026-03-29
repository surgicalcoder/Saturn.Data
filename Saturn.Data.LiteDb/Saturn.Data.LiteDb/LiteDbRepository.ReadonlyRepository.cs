using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await GetCollection<TItem>().FindById(id, cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        var results = GetCollection<TItem>().Find(entity => IDs.Contains(entity.Id), cancellationToken: cancellationToken);

        return results;
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable().Where(predicate);
        query = ApplyContinueFrom(query, continueFrom);

        return await query.ToAsyncEnumerable().LongCountAsync(cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return GetCollection<TItem>().FindAll(cancellationToken);
    }

    public virtual IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return GetCollection<TItem>().AsQueryable();
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable().Where(predicate);
        query = ApplyContinueFrom(query, continueFrom);
        query = ApplySortOrders(query, sortOrders);
        query = ApplyPagination(query, pageSize, pageNumber);

        return Task.FromResult(query.ToAsyncEnumerable());
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable();

        foreach (var clause in whereClause)
        {
            var lambda = BuildWhereClausePredicate<TItem>(clause.Key, clause.Value);
            query = query.Where(lambda);
        }

        query = ApplyContinueFrom(query, continueFrom);
        query = ApplySortOrders(query, sortOrders);
        query = ApplyPagination(query, pageSize, pageNumber);
    
        return query.ToAsyncEnumerable();
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable().Where(predicate);
        query = ApplyContinueFrom(query, continueFrom);
        query = ApplySortOrders(query, sortOrders);

        return await query.ToAsyncEnumerable().FirstOrDefaultAsync(cancellationToken);
    }

    private static Expression<Func<TItem, bool>> BuildWhereClausePredicate<TItem>(string propertyName, object value) where TItem : Entity
    {
        var parameter = Expression.Parameter(typeof(TItem), "entity");
        Expression property = Expression.Property(parameter, propertyName);
        object comparisonValue = value;

        var propertyType = property.Type;

        if (TryGetEntityIdProperty(propertyType, out var idProperty) && value is string)
        {
            property = Expression.Property(property, idProperty);
            propertyType = property.Type;
        }

        if (comparisonValue != null && propertyType != comparisonValue.GetType())
        {
            comparisonValue = Convert.ChangeType(comparisonValue, Nullable.GetUnderlyingType(propertyType) ?? propertyType);
        }

        var constant = Expression.Constant(comparisonValue, propertyType);
        var equal = Expression.Equal(property, constant);
        return Expression.Lambda<Func<TItem, bool>>(equal, parameter);
    }

    private static bool TryGetEntityIdProperty(Type type, out string idProperty)
    {
        idProperty = null;

        if (typeof(Entity).IsAssignableFrom(type))
        {
            idProperty = "Id";
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>))
        {
            idProperty = "Id";
            return true;
        }

        return false;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var query = collection.Query();
        
        if (predicate != null)
        {
            var predExpr = BsonMapper.Global.GetExpression(predicate);
            query = query.Where(predExpr);
        }
        
        var items = query.OrderBy(x => Guid.NewGuid()).Limit(count);

        return items.ToEnumerable(cancellationToken);
    }
    
}
