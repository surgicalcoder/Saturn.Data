using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository //: IRepository
{
    protected LiteDatabase database;
    protected LiteDBRepositoryOptions liteDbOptions;
    protected RepositoryOptions options;
    protected BsonMapper mapper;

    public LiteDbRepository(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    {
        liteDbOptions = liteDbRepositoryOptions;
        mapper = liteDbRepositoryOptions.Mapper;

   //     RegisterAllEntityRefs(mapper, liteDbRepositoryOptions.AdditionalAssembliesToScanForRefs);

        BsonMapper.Global = mapper;

        database = new LiteDatabase(liteDbOptions.ConnectionString, mapper);
        options = repositoryOptions;
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public async Task Rebuild()
    {
        await database.Rebuild();
    }
    
    public void Dispose()
    {
        database?.Dispose();
    }

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException();
    }

    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }

    protected virtual ILiteCollection<T> GetCollection<T>() where T : Entity
    {
        return database.GetCollection<T>(GetCollectionNameForType<T>());
    }
    
    
    /*
    private Expression<Func<TItem, bool>> TransformRefEntityComparisons<TItem>(Expression<Func<TItem, bool>> predicate) where TItem : Entity
    {
        return (Expression<Func<TItem, bool>>)new RefEqualityExpressionVisitor().Visit(predicate);
    }
    
    private class RefEqualityExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Handle logical AND operations - break them apart and process individually
            if (node.NodeType is ExpressionType.AndAlso or ExpressionType.And)
            {
                var leftVisited = Visit(node.Left);
                var rightVisited = Visit(node.Right);
                
                return Expression.MakeBinary(node.NodeType, leftVisited, rightVisited, node.IsLiftedToNull, node.Method);
            }
            
            // Process equality expressions
            if (node.NodeType != ExpressionType.Equal && node.NodeType != ExpressionType.NotEqual)
                return base.VisitBinary(node);
    
            var left = Visit(node.Left);
            var right = Visit(node.Right);
    
            
            // Case 0: Check if right is a Convert node with an entity inside
            if (right is UnaryExpression unaryExpr && unaryExpr.NodeType == ExpressionType.Convert &&
                IsEntityType(unaryExpr.Operand.Type) && !IsRefType(unaryExpr.Operand.Type))
            {
                // Replace with a comparison to the entity's Id property
                var idProperty = Expression.Property(unaryExpr.Operand, "Id");
                // Ensure type compatibility by converting if needed
                if (idProperty.Type != left.Type)
                {
                    right = Expression.Convert(idProperty, left.Type);
                }
                else
                {
                    right = idProperty;
                }
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
            
            /*
            // Case 1: left is Ref<T> and right is a T entity
            if (IsRefType(left.Type) && IsEntityType(right.Type) && !IsRefType(right.Type))
            {
                // Replace right with right.Id
                right = Expression.Property(right, "Id");
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
            
            // Case 2: right is Ref<T> and left is a T entity
            if (IsRefType(right.Type) && IsEntityType(left.Type) && !IsRefType(left.Type))
            {
                // Replace left with left.Id
                left = Expression.Property(left, "Id");
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
            
            // Case 3: both are entity objects (not Ref<T>)
            if (IsEntityType(left.Type) && !IsRefType(left.Type) && 
                IsEntityType(right.Type) && !IsRefType(right.Type))
            {
                left = Expression.Property(left, "Id");
                right = Expression.Property(right, "Id");
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
            
            // Case 4: both are Ref<T> - no transformation needed as they compare correctly
            
            // No transformation needed
            if (left != node.Left || right != node.Right)
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
                #1#
                
            return node;
        }
        
        private static bool IsRefType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>);
        }
        
        private static bool IsEntityType(Type type)
        {
            return typeof(Entity).IsAssignableFrom(type);
        }
    }
    */
    
    
    
    
    
    /// <summary>
    /// Applies sort orders to a LiteDB async query builder
    /// </summary>
    internal virtual ILiteQueryable<TItem> ApplySortOrders<TItem>(ILiteQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders == null) return query;

        return sortOrders.Aggregate(query, (current, sortOrder) =>
            sortOrder.Direction == SortDirection.Ascending
                ? current.OrderBy(sortOrder.Field)
                : current.OrderByDescending(sortOrder.Field));
    }

    /// <summary>
    /// Applies sort orders to a LINQ queryable
    /// </summary>
    internal virtual IQueryable<TItem> ApplySortOrders<TItem>(IQueryable<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
    {
        if (sortOrders == null) return query;

        return sortOrders.Aggregate(query, (current, sortOrder) =>
            sortOrder.Direction == SortDirection.Ascending
                ? current.OrderBy(sortOrder.Field)
                : current.OrderByDescending(sortOrder.Field));
    }

    /// <summary>
    /// Creates a LiteDB async query with predicate and continueFrom logic.
    /// Continuation tokens are always interpreted as canonical ObjectId-based entity IDs.
    /// Paging remains stable only when the caller sorts by <c>Id</c> ascending.
    /// </summary>
    internal virtual ILiteQueryable<TItem> BuildQuery<TItem>(ILiteCollection<TItem> collection, Expression<Func<TItem, bool>> predicate, string? continueFrom = null) where TItem : Entity
    {
        var query = collection.Query().Where(BsonMapper.Global.GetExpression(predicate));

        if (!TryNormalizeContinuationToken(continueFrom, out var continuationToken, out _))
        {
            return query;
        }

        return query.Where(Query.GT("_id", new BsonValue(continuationToken)));
    }

    /// <summary>
    /// Builds a predicate expression with optional continuation token support.
    /// Invalid continuation tokens are ignored so callers can handle them gracefully.
    /// </summary>
    internal virtual BsonExpression BuildExpressionWithContinuation<TItem>(Expression<Func<TItem, bool>> predicate, string? continueFrom = null) where TItem : Entity
    {
        return BsonMapper.Global.GetExpression(predicate);
    }

    /// <summary>
    /// Applies continueFrom logic to a LINQ queryable.
    /// Continuation tokens are compared using their canonical ObjectId hex string representation.
    /// </summary>
    internal virtual IQueryable<TItem> ApplyContinueFrom<TItem>(IQueryable<TItem> query, string? continueFrom) where TItem : Entity
    {
        if (!TryNormalizeContinuationToken(continueFrom, out _, out var normalizedContinuationToken))
        {
            return query;
        }

        return query.Where(x => string.Compare(x.Id, normalizedContinuationToken) > 0);
    }

    /// <summary>
    /// Applies pagination to a LiteDB query.
    /// <paramref name="pageNumber"/> is 1-based when supplied together with <paramref name="pageSize"/>.
    /// </summary>
    internal virtual ILiteQueryableResult<TItem> ApplyPagination<TItem>(ILiteQueryable<TItem> query, int? pageSize, int? pageNumber = null) where TItem : Entity
    {
        if (pageSize is not > 0)
        {
            return query;
        }

        if (pageNumber is > 1)
        {
            return query.Offset((pageNumber.Value - 1) * pageSize.Value).Limit(pageSize.Value);
        }

        return query.Limit(pageSize.Value);
    }

    /// <summary>
    /// Applies pagination to a queryable.
    /// <paramref name="pageNumber"/> is 1-based when supplied together with <paramref name="pageSize"/>.
    /// </summary>
    internal virtual IQueryable<TItem> ApplyPagination<TItem>(IQueryable<TItem> query, int? pageSize, int? pageNumber = null) where TItem : Entity
    {
        if (pageSize is not > 0)
        {
            return query;
        }

        if (pageNumber is > 1)
        {
            query = query.Skip((pageNumber.Value - 1) * pageSize.Value);
        }

        return query.Take(pageSize.Value);
    }

    /// <summary>
    /// Tries to normalize a continuation token into the canonical ObjectId hex string used by entity IDs.
    /// </summary>
    internal virtual bool TryNormalizeContinuationToken(string? continueFrom, out ObjectId continuationToken, out string? normalizedContinuationToken)
    {
        continuationToken = ObjectId.Empty;
        normalizedContinuationToken = null;

        if (string.IsNullOrWhiteSpace(continueFrom))
        {
            return false;
        }

        try
        {
            continuationToken = new ObjectId(continueFrom);
            normalizedContinuationToken = continuationToken.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    
}