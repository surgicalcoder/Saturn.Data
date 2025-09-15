using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDB;
using LiteDB.Async;
using LiteDB.Engine;

namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository //: IRepository
{
    protected LiteDatabaseAsync database;
    protected LiteDBRepositoryOptions liteDbOptions;
    protected RepositoryOptions options;
    protected BsonMapper mapper;

    public LiteDbRepository(RepositoryOptions repositoryOptions, LiteDBRepositoryOptions liteDbRepositoryOptions)
    {
        liteDbOptions = liteDbRepositoryOptions;
        mapper = liteDbRepositoryOptions.Mapper;

        RegisterAllEntityRefs(mapper, liteDbRepositoryOptions.AdditionalAssembliesToScanForRefs);

        BsonMapper.Global = mapper;

        database = new LiteDatabaseAsync(liteDbOptions.ConnectionString, mapper);
        options = repositoryOptions;
    }

    protected virtual ConcurrentDictionary<string, string> typeNameCache { get; set; } = new();

    public async Task Rebuild()
    {
        await database.RebuildAsync();
    }
    
    public void Dispose()
    {
        database?.Dispose();
    }

    public async Task<IDatabaseTransaction> CreateTransaction()
    {
        throw new NotImplementedException();
    }

    protected virtual void RegisterAllEntityRefs(BsonMapper mapper, Assembly[] additionalAssembliesToScanForRefs)
    {
        var entityBase = typeof(Entity);
        var openRef = typeof(Ref<>);

        // 1) find every non-abstract Entity subclass with a public parameterless ctor
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(additionalAssembliesToScanForRefs ?? []);
        
        var entityTypes = assemblies
                                   .SelectMany(a =>
                                   {
                                       try
                                       {
                                           return a.GetTypes();
                                       }
                                       catch
                                       {
                                            return [];
                                       }
                                   })
                                   .Where(t =>
                                       entityBase.IsAssignableFrom(t) &&
                                       !t.IsAbstract &&
                                       t.GetConstructor(Type.EmptyTypes) != null
                                   );

        foreach (var entityType in entityTypes)
        {
            // 2) construct the Ref<ThatEntity> type
            var refType = openRef.MakeGenericType(entityType);
            var idProp = refType.GetProperty("Id")!;

            // 3) serializer: take your Ref<ThatEntity>.Id → raw BsonValue
            Func<object, BsonValue> serialize = obj =>
            {
                var idVal = idProp.GetValue(obj);

                return BsonMapper.Global.Serialize(idVal.GetType(), idVal);
            };

            // 4) deserializer: raw BsonValue → new Ref<ThatEntity> { Id = … }
            Func<BsonValue, object> deserialize = bson =>
            {
                var inst = Activator.CreateInstance(refType)!;
                var clrId = BsonMapper.Global.Deserialize(idProp.PropertyType, bson);
                idProp.SetValue(inst, clrId);

                return inst;
            };

            // 5) register it
            mapper.RegisterType(refType, serialize, deserialize);
        }
    }

    protected virtual string GetCollectionNameForType<T>()
    {
        return typeNameCache.GetOrAdd(typeof(T).FullName, s => options.GetCollectionName.Invoke(typeof(T)));
    }

    protected virtual ILiteCollectionAsync<T> GetCollection<T>() where T : Entity
    {
        return database.GetCollection<T>(GetCollectionNameForType<T>());
    }
    
    
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
                */
                
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
    
    
    
    
    
    /// <summary>
    /// Applies sort orders to a LiteDB async query builder
    /// </summary>
    internal virtual LiteDB.Async.ILiteQueryableAsync<TItem> ApplySortOrders<TItem>(LiteDB.Async.ILiteQueryableAsync<TItem> query, IEnumerable<SortOrder<TItem>> sortOrders) where TItem : Entity
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
    /// Creates a LiteDB async query with predicate and continueFrom logic
    /// </summary>
    internal virtual LiteDB.Async.ILiteQueryableAsync<TItem> BuildQuery<TItem>(LiteDB.Async.ILiteCollectionAsync<TItem> collection, Expression<Func<TItem, bool>> predicate, string continueFrom = null) where TItem : Entity
    {
        var predExpr = BsonMapper.Global.GetExpression(predicate);
        
        if (string.IsNullOrEmpty(continueFrom))
        {
            return collection.Query().Where(predExpr);
        }

        var andExpr = Query.And(predExpr, Query.GT("_id", new BsonValue(new ObjectId(continueFrom))));
        return collection.Query().Where(andExpr);
    }

    /// <summary>
    /// Applies continueFrom logic to a LINQ queryable
    /// </summary>
    internal virtual IQueryable<TItem> ApplyContinueFrom<TItem>(IQueryable<TItem> query, string continueFrom) where TItem : Entity
    {
        if (string.IsNullOrEmpty(continueFrom)) return query;
        
        return query.Where(x => string.Compare(x.Id, continueFrom) > 0);
    }

    /// <summary>
    /// Applies pagination to a queryable
    /// </summary>
    internal virtual IQueryable<TItem> ApplyPagination<TItem>(IQueryable<TItem> query, int? pageSize) where TItem : Entity
    {
        if (pageSize is > 0)
        {
            return query.Take(pageSize.Value);
        }
        return query;
    }

    
}