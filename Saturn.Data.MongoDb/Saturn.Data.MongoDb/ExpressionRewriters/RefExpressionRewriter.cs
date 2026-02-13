using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;

namespace Saturn.Data.MongoDb.ExpressionRewriters;

public static class RefExpressionRewriter
{
    public static Expression<Func<T, bool>> NormalizeForRef<T>(this Expression<Func<T, bool>> expr)
    {
        var visitor = new RefExpressionVisitor();
        var newBody = visitor.Visit(expr.Body);
        return Expression.Lambda<Func<T, bool>>(newBody, expr.Parameters);
    }

    private class RefExpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual)
            {
                var left = node.Left;
                var right = node.Right;

                // Detect Ref<T>.Id == string comparisons
                // Problem: Ref<T>.Id is stored as ObjectId in MongoDB, but is a string in C#
                // Solution: Rewrite to compare the Ref object itself, not just the Id
                if (IsRefId(left) && IsString(right))
                {
                    // Change from: refObj.Id == stringId
                    // To: refObj == new Ref<T>(stringId)
                    var memberExpr = (MemberExpression)left;
                    var refExpr = Visit(memberExpr.Expression); // The Ref<T> object
                    var stringExpr = Visit(right); // The string Id
                    
                    // Create new Ref<T>(stringId)
                    var refType = refExpr.Type;
                    var refCtor = refType.GetConstructor(new[] { typeof(string) });
                    var newRefExpr = Expression.New(refCtor!, stringExpr);
                    
                    return node.NodeType == ExpressionType.Equal 
                        ? Expression.Equal(refExpr, newRefExpr)
                        : Expression.NotEqual(refExpr, newRefExpr);
                }

                if (IsRefId(right) && IsString(left))
                {
                    // Change from: stringId == refObj.Id
                    // To: new Ref<T>(stringId) == refObj
                    var memberExpr = (MemberExpression)right;
                    var refExpr = Visit(memberExpr.Expression); // The Ref<T> object
                    var stringExpr = Visit(left); // The string Id
                    
                    // Create new Ref<T>(stringId)
                    var refType = refExpr.Type;
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
            // Handle Count, Any, Where, etc. with lambda predicates
            // These methods need their lambda expressions to be visited so nested Ref<T>.Id comparisons are rewritten
            if (node.Method.DeclaringType == typeof(Enumerable) || 
                (node.Method.DeclaringType?.IsGenericType == true && 
                 node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (node.Method.Name is "Count" or "Any" or "Where" or "First" or "FirstOrDefault" or "Single" or "SingleOrDefault")
                {
                    // These methods may have a lambda predicate as the last argument - visit it
                    return base.VisitMethodCall(node);
                }
            }
            
            // Handle .Contains(...) — works for both lists and arrays
            if (node.Method.Name == nameof(Enumerable.Contains))
            {
                // Enumerable.Contains(collection, value)
                var collectionExpr = node.Arguments.FirstOrDefault();
                var valueExpr = node.Arguments.LastOrDefault();

                var visitedCollection = Visit(collectionExpr);
                var visitedValue = Visit(valueExpr);

                // Check if we actually need to modify anything
                bool needsModification = false;

                // Normalize if value is a string but the collection holds Ref<T>.Id (ObjectIds)
                if (IsRefId(valueExpr))
                {
                    if (IsStringCollection(visitedCollection))
                    {
                        // Wrap string array into ObjectIds
                        visitedCollection = ConvertStringCollectionToObjectId(visitedCollection);
                        needsModification = true;
                    }
                }
                else if (IsString(visitedValue))
                {
                    // Example: ids.Contains(d.Id)
                    if (IsRefId(valueExpr))
                    {
                        visitedValue = WrapAsObjectId(visitedValue);
                        needsModification = true;
                    }
                }

                // Only reconstruct the call if we made modifications and the method is generic
                if (needsModification && node.Method.IsGenericMethod)
                {
                    return Expression.Call(
                        node.Method.GetGenericMethodDefinition().MakeGenericMethod(visitedValue.Type),
                        visitedCollection,
                        visitedValue);
                }
                
                // If no modifications or not a generic method, just return the visited version
                if (visitedCollection != collectionExpr || visitedValue != valueExpr)
                {
                    // Arguments changed but method is not generic - recreate with same method
                    return node.Update(node.Object, new[] { visitedCollection, visitedValue });
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

        private static bool IsString(Expression expr) => expr.Type == typeof(string);

        private static bool IsStringCollection(Expression expr)
        {
            var t = expr.Type;
            return typeof(IEnumerable<string>).IsAssignableFrom(t);
        }

        private static Expression WrapAsObjectId(Expression stringExpr)
        {
            // Use ObjectId.Parse instead of constructor - MongoDB LINQ provider supports this
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