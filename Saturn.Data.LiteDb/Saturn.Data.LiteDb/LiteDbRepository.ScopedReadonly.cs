using System.Linq.Expressions;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using LiteDbX;
using Saturn.Data.LiteDb; // For ObjectId
using SortDirection = GoLive.Saturn.Data.Abstractions.SortDirection;


namespace Saturn.Data.LiteDb;

public partial class LiteDbRepository : IScopedReadonlyRepository
{
    
    
    
  /// <summary>
/// Rewrites any comparison of e.Scope == someRef (where Scope is Ref&lt;T&gt;)
/// to e.Scope.Id == someRef.Id (letting LiteDB compare the underlying string IDs)
/// </summary>
private class ScopeObjectIdRewriter : ExpressionVisitor
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType != ExpressionType.Equal && node.NodeType != ExpressionType.NotEqual)
            return base.VisitBinary(node);

        static Expression Unwrap(Expression expr)
        {
            while (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;
            return expr;
        }

        var leftUnwrapped  = Unwrap(node.Left);
        var rightUnwrapped = Unwrap(node.Right);

        bool leftIsRef    = IsRefType(leftUnwrapped.Type);
        bool rightIsRef   = IsRefType(rightUnwrapped.Type);
        bool leftIsString  = leftUnwrapped.Type == typeof(string);
        bool rightIsString = rightUnwrapped.Type == typeof(string);

        // Left is Ref<T>, right is a string (constant OR closure-captured field)
        if (leftIsRef && rightIsString)
        {
            var leftId       = GetIdExpression(leftUnwrapped);
            var objectIdExpr = MakeObjectIdExpression(rightUnwrapped);
            return Expression.MakeBinary(node.NodeType, leftId, objectIdExpr);
        }

        // Right is Ref<T>, left is a string (constant OR closure-captured field)
        if (rightIsRef && leftIsString)
        {
            var rightId      = GetIdExpression(rightUnwrapped);
            var objectIdExpr = MakeObjectIdExpression(leftUnwrapped);
            return Expression.MakeBinary(node.NodeType, objectIdExpr, rightId);
        }

        // Both sides are Ref<T>
        if (leftIsRef && rightIsRef)
        {
            var leftId  = GetIdExpression(leftUnwrapped);
            var rightId = GetIdExpression(rightUnwrapped);
            return Expression.MakeBinary(node.NodeType, leftId, rightId);
        }

        return base.VisitBinary(node);
    }

    private static Expression GetIdExpression(Expression refExpr)
    {
        var idProperty = refExpr.Type.GetProperty("Id")
            ?? throw new InvalidOperationException($"Type {refExpr.Type.Name} does not have an 'Id' property.");
        return Expression.Property(refExpr, idProperty);
    }

    private static Expression MakeObjectIdExpression(Expression stringExpr)
    {
        var ctor = typeof(ObjectId).GetConstructor(new[] { typeof(string) })
            ?? throw new InvalidOperationException("ObjectId(string) constructor not found.");
        return Expression.New(ctor, stringExpr);
    }

    private static bool IsRefType(Type type)
    {
        if (type == null || !type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        return def.FullName != null && def.FullName.StartsWith("GoLive.Saturn.Data.Entities.Ref`1");
    }
}
    
    
    
    
    
    

    private static Expression<Func<TItem, bool>> RewriteScopeComparisons<TItem>(Expression<Func<TItem, bool>> predicate)
    {
        return (Expression<Func<TItem, bool>>)new ScopeObjectIdRewriter().Visit(predicate);
    }
    public virtual async Task<TItem> ById<TItem, TScope>(string scope, string id, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }
        // Rewrite scope comparison to ObjectId
        Expression<Func<TItem, bool>> pred = e => e.Id == id && e.Scope == scope;
        pred = RewriteScopeComparisons(pred);
        return await GetCollection<TItem>().FindOne(pred, cancellationToken);
    }

    public async Task<IAsyncEnumerable<TItem>> ById<TItem, TScope>(string scope, IEnumerable<string> IDs, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var result = GetCollection<TItem>().Find(e => IDs.Contains(e.Id) && e.Scope == scope, cancellationToken: cancellationToken);

        return result;
    }


    public virtual Task<IAsyncEnumerable<TItem>> All<TItem, TScope>(string scope, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = default) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }
        Expression<Func<TItem, bool>> pred = f => f.Scope == scope;
        pred = RewriteScopeComparisons(pred);
        var scopedEntities = GetCollection<TItem>().AsQueryable().Where(pred);
        return Task.FromResult(scopedEntities.ToAsyncEnumerable());
    }

    public virtual IQueryable<TItem> IQueryable<TItem, TScope>(string scope) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        Expression<Func<TItem, bool>> pred = f => f.Scope == scope;
        pred = RewriteScopeComparisons(pred);
        return GetCollection<TItem>().AsQueryable().Where(pred);
    }

    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);
        combinedPredicate = RewriteScopeComparisons(combinedPredicate);
        return await Many<TItem>(combinedPredicate, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    public async Task<IAsyncEnumerable<TItem>> Many<TItem, TScope>(string scope, Dictionary<string, object> whereClause, string continueFrom = null, int? pageSize = 20, int? pageNumber = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedWhereClause = whereClause ?? new Dictionary<string, object>();
        combinedWhereClause["Scope"] = scope;
        return await Many<TItem>(combinedWhereClause, continueFrom, pageSize, pageNumber, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<TItem> One<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IEnumerable<SortOrder<TItem>> sortOrders = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate.And<TItem>(e => e.Scope == scope);
        combinedPredicate = RewriteScopeComparisons(combinedPredicate);
        return await One<TItem>(combinedPredicate, continueFrom, sortOrders, transaction, cancellationToken);
    }
    
    public async Task<long> Count<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate, string continueFrom = null, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope))
        {
            return 0;
        }

        Expression<Func<TItem, bool>> firstPred = item => item.Scope == scope;
        var combinedPred = firstPred.And(predicate);
        combinedPred = RewriteScopeComparisons(combinedPred);
        return await GetCollection<TItem>().LongCount(combinedPred, cancellationToken);
    }
    
    public async Task<IAsyncEnumerable<TItem>> Random<TItem, TScope>(string scope, Expression<Func<TItem, bool>> predicate = null, string continueFrom = null, int count = 1, IDatabaseTransaction transaction = null, CancellationToken cancellationToken = new CancellationToken()) where TItem : ScopedEntity<TScope>, new() where TScope : Entity, new()
    {
        var combinedPredicate = predicate == null ? (item => item.Scope == scope) : predicate.And<TItem>(e => e.Scope == scope);
        combinedPredicate = RewriteScopeComparisons(combinedPredicate);
        return await Random<TItem>(combinedPredicate, continueFrom, count, transaction, cancellationToken);
    }
}