using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository : IReadonlyRepository
{
    public virtual async Task<TItem> ById<TItem>(string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await ById<TItem>(id, includeDeleted: false, transaction, cancellationToken);
    }

    public virtual async Task<TItem> ById<TItem>(string id, bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var item = await GetCollection<TItem>().FindById(id, cancellationToken);

        if (includeDeleted || item == null || item is not ISoftDeletable deletable || !deletable.IsDeleted)
        {
            return item;
        }

        return null;
    }

    public virtual async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await ById<TItem>(IDs, includeDeleted: false, transaction, cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> ById<TItem>(IEnumerable<string> IDs, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = default) where TItem : Entity
    {
        var normalizedIds = NormalizeEntityIds(IDs);

        if (normalizedIds.Count == 0)
        {
            return EmptyAsyncEnumerable<TItem>();
        }

        var predicate = ApplySoftDeleteFilter<TItem>(entity => true, includeDeleted);

        var results = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(predicate))
            .Where(Query.In("_id", normalizedIds))
            .ToEnumerable(cancellationToken);

        return results;
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Count(predicate, continueFrom, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<long> Count<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        var query = BuildQuery(collection, ApplySoftDeleteFilter(predicate, includeDeleted), continueFrom);

        return await query.LongCount(cancellationToken);
    }

    public virtual async Task<IAsyncEnumerable<TItem>> All<TItem>(IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        return await All<TItem>(includeDeleted: false, transaction, cancellationToken);
    }

    public virtual Task<IAsyncEnumerable<TItem>> All<TItem>(bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default)
        where TItem : Entity
    {
        var predicate = ApplySoftDeleteFilter<TItem>(entity => true, includeDeleted);
        var items = GetCollection<TItem>().Query().Where(BsonMapper.Global.GetExpression(predicate)).ToEnumerable(cancellationToken);
        return Task.FromResult(items);
    }

    public virtual IQueryable<TItem> IQueryable<TItem>() where TItem : Entity
    {
        return IQueryable<TItem>(includeDeleted: false);
    }

    public virtual IQueryable<TItem> IQueryable<TItem>(bool includeDeleted = false) where TItem : Entity
    {
        var query = GetCollection<TItem>().AsQueryable();

        if (includeDeleted || !SupportsSoftDelete<TItem>())
        {
            return query;
        }

        return query.Where(BuildNotDeletedPredicate<TItem>());
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return Many<TItem>(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public Task<IAsyncEnumerable<TItem>> Many<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null,
        IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false, IDatabaseTransaction transaction = null,
        CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        var effectiveContinueFrom = CanApplyContinuation(sortOrders) ? continueFrom : null;

        var query = BuildQuery(collection, ApplySoftDeleteFilter(predicate, includeDeleted), effectiveContinueFrom);
        query = ApplySortOrders(query, sortOrders);
        var finalQuery = ApplyPagination(query, pageSize, pageNumber);

        return Task.FromResult(finalQuery.ToEnumerable(cancellationToken));
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Many<TItem>(whereClause, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem>(Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20,
        int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        Expression<Func<TItem, bool>> predicate = entity => true;

        foreach (var clause in whereClause)
        {
            predicate = predicate.And(BuildWhereClausePredicate<TItem>(clause.Key, clause.Value));
        }

        predicate = ApplySoftDeleteFilter(predicate, includeDeleted);

        return await Many(predicate, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await One<TItem>(predicate, continueFrom, sortOrders, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<TItem> One<TItem>(Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var effectiveContinueFrom = CanApplyContinuation(sortOrders) ? continueFrom : null;
        var results = await Many(predicate, effectiveContinueFrom, 1, null, sortOrders, includeDeleted, transaction, cancellationToken);

        return await results.FirstOrDefaultAsync(cancellationToken);
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

        if (typeof(Entity).IsAssignableFrom(type) || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>))
        {
            idProperty = "Id";
            return true;
        }

        return false;
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        return await Random(predicate, continueFrom, count, includeDeleted: false, transaction, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> Random<TItem>(Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : Entity
    {
        var collection = GetCollection<TItem>();
        
        var query = collection.Query();

        var effectivePredicate = predicate ?? (entity => true);
        effectivePredicate = ApplySoftDeleteFilter(effectivePredicate, includeDeleted);
        query = query.Where(BsonMapper.Global.GetExpression(effectivePredicate));
        
        var items = query.OrderBy(x => Guid.NewGuid()).Limit(count);

        return items.ToEnumerable(cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> All<TItem, TProjection>(Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var predicate = ApplySoftDeleteFilter<TItem>(entity => true, includeDeleted);
        var items = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(predicate))
            .Select(selector)
            .ToEnumerable(cancellationToken);

        return Task.FromResult(items);
    }

    public async Task<TProjection> ById<TItem, TProjection>(string id, Expression<Func<TItem, TProjection>> selector, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var query = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(ApplySoftDeleteFilter<TItem>(entity => entity.Id == id, includeDeleted)))
            .Select(selector)
            .Limit(1)
            .ToEnumerable(cancellationToken);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IAsyncEnumerable<TProjection>> ById<TItem, TProjection>(IEnumerable<string> ids, Expression<Func<TItem, TProjection>> selector,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var normalizedIds = NormalizeEntityIds(ids);

        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(EmptyAsyncEnumerable<TProjection>());
        }

        var query = GetCollection<TItem>()
            .Query()
            .Where(BsonMapper.Global.GetExpression(ApplySoftDeleteFilter<TItem>(entity => true, includeDeleted)))
            .Where(Query.In("_id", normalizedIds))
            .Select(selector)
            .ToEnumerable(cancellationToken);

        return Task.FromResult(query);
    }

    public Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var collection = GetCollection<TItem>();
        var effectiveContinueFrom = CanApplyContinuation(sortOrders) ? continueFrom : null;

        var query = BuildQuery(collection, ApplySoftDeleteFilter(predicate, includeDeleted), effectiveContinueFrom);
        query = ApplySortOrders(query, sortOrders);

        var projected = query.Select(selector);
        var finalQuery = ApplyPagination(projected, pageSize, pageNumber);

        return Task.FromResult(finalQuery.ToEnumerable(cancellationToken));
    }

    public async Task<IAsyncEnumerable<TProjection>> Many<TItem, TProjection>(Dictionary<string, object> whereClause, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null,
        bool includeDeleted = false, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression<Func<TItem, bool>> predicate = entity => true;

        foreach (var clause in whereClause)
        {
            predicate = predicate.And(BuildWhereClausePredicate<TItem>(clause.Key, clause.Value));
        }

        return await Many(predicate, selector, continueFrom, pageSize, pageNumber, sortOrders, includeDeleted, transaction, cancellationToken);
    }

    public async Task<TProjection> One<TItem, TProjection>(Expression<Func<TItem, bool>> predicate, Expression<Func<TItem, TProjection>> selector,
        string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, bool includeDeleted = false,
        IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : Entity
    {
        ArgumentNullException.ThrowIfNull(selector);

        var results = await Many(predicate, selector, continueFrom, 1, null, sortOrders, includeDeleted, transaction, cancellationToken);
        return await results.FirstOrDefaultAsync(cancellationToken);
    }
    
}
