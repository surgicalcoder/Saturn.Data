using System.Diagnostics;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;

namespace Saturn.Data.MongoDb.ExpressionRewriters;

public static class RefExpressionRewriter
{
    public static Expression<Func<T, bool>> NormalizeForRef<T>(this Expression<Func<T, bool>> expr)
    {
        if (expr is null)
        {
            throw new ArgumentNullException(nameof(expr),
                "NRE in NormalizeForRef: predicate expression is null");
        }

        Debug.WriteLine($"[RefExprRewriter] NormalizeForRef<{typeof(T).Name}>: {expr}");

        try
        {
            var visitor = new RefExpressionVisitor();
            var newBody = visitor.Visit(expr.Body);
            return Expression.Lambda<Func<T, bool>>(newBody, expr.Parameters);
        }
        catch (NullReferenceException)
        {
            throw new InvalidOperationException(
                $"NRE inside NormalizeForRef visitor. Expression: {expr}");
        }
    }

    private class RefExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                var left = node.Left;
                var right = node.Right;

                Debug.WriteLineIf(IsRefId(left) || IsRefId(right),
                    $"[RefExprRewriter] VisitBinary rewriting at {node}");

                if (IsRefId(left) && IsString(right))
                {
                    var memberExpr = (MemberExpression)left;
                    var refExpr = Visit(memberExpr.Expression);
                    var stringExpr = Visit(right);

                    Debug.WriteLineIf(refExpr is null,
                        $"[RefExprRewriter] VisitBinary: refExpr is null for {memberExpr.Expression}");

                    var refType = refExpr!.Type;
                    var refCtor = refType.GetConstructor(new[] { typeof(string) });

                    var newRefExpr = Expression.New(refCtor!, stringExpr);

                    return node.NodeType == ExpressionType.Equal
                        ? Expression.Equal(refExpr, newRefExpr)
                        : Expression.NotEqual(refExpr, newRefExpr);
                }

                if (IsRefId(right) && IsString(left))
                {
                    var memberExpr = (MemberExpression)right;
                    var refExpr = Visit(memberExpr.Expression);
                    var stringExpr = Visit(left);

                    Debug.WriteLineIf(refExpr is null,
                        $"[RefExprRewriter] VisitBinary: refExpr is null for {memberExpr.Expression}");

                    var refType = refExpr!.Type;
                    var refCtor = refType.GetConstructor(new[] { typeof(string) });

                    var newRefExpr = Expression.New(refCtor!, stringExpr);

                    return node.NodeType == ExpressionType.Equal
                        ? Expression.Equal(newRefExpr, refExpr)
                        : Expression.NotEqual(newRefExpr, refExpr);
                }
            }

            return base.VisitBinary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Enumerable) ||
                (node.Method.DeclaringType?.IsGenericType == true &&
                 node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (node.Method.Name is "Count" or "Any" or "Where" or "First" or "FirstOrDefault" or "Single" or "SingleOrDefault")
                {
                    return base.VisitMethodCall(node);
                }
            }

            if (node.Method.Name == nameof(Enumerable.Contains))
            {
                var collectionExpr = node.Arguments.FirstOrDefault();
                var valueExpr = node.Arguments.LastOrDefault();

                Debug.WriteLine($"[RefExprRewriter] VisitMethodCall Contains: col={collectionExpr}, val={valueExpr}");

                var visitedCollection = Visit(collectionExpr);
                var visitedValue = Visit(valueExpr);

                var needsModification = false;

                if (IsRefId(valueExpr))
                {
                    if (visitedCollection != null && IsStringCollection(visitedCollection))
                    {
                        visitedCollection = ConvertStringCollectionToObjectId(visitedCollection);
                        needsModification = true;
                    }
                }
                else if (visitedValue != null && IsString(visitedValue))
                {
                    if (IsRefId(valueExpr))
                    {
                        visitedValue = WrapAsObjectId(visitedValue);
                        needsModification = true;
                    }
                }

                if (needsModification && node.Method.IsGenericMethod)
                {
                    return Expression.Call(
                        node.Method.GetGenericMethodDefinition().MakeGenericMethod(visitedValue!.Type),
                        visitedCollection,
                        visitedValue);
                }

                if (visitedCollection != collectionExpr || visitedValue != valueExpr)
                {
                    return node.Update(node.Object, new[] { visitedCollection!, visitedValue! });
                }
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsRefId(Expression expr)
        {
            return expr is MemberExpression m &&
                   m.Member.Name == "Id" &&
                   m.Expression?.Type.IsGenericType == true &&
                   m.Expression.Type.GetGenericTypeDefinition() == typeof(Ref<>);
        }

        private static bool IsString(Expression expr) =>
            expr?.Type == typeof(string);

        private static bool IsStringCollection(Expression expr)
        {
            var t = expr?.Type;
            return t != null && typeof(IEnumerable<string>).IsAssignableFrom(t);
        }

        private static Expression WrapAsObjectId(Expression stringExpr)
        {
            var parseMethod = typeof(ObjectId).GetMethod(nameof(ObjectId.Parse), new[] { typeof(string) });
            return Expression.Call(parseMethod!, stringExpr);
        }

        private static Expression ConvertStringCollectionToObjectId(Expression collectionExpr)
        {
            var stringType = typeof(string);
            var objectIdType = typeof(ObjectId);
            var parseMethod = objectIdType.GetMethod(nameof(ObjectId.Parse), new[] { stringType });

            var param = Expression.Parameter(stringType, "s");
            var convertExpr = Expression.Call(parseMethod!, param);
            var selectMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                .MakeGenericMethod(stringType, objectIdType);

            return Expression.Call(
                selectMethod,
                collectionExpr,
                Expression.Lambda(convertExpr, param));
        }
    }
}
