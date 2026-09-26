using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Query;

public sealed class SqliteExpressionTranslator
{
    private int parameterIndex;

    public SqlFragment Translate(LambdaExpression predicate)
    {
        parameterIndex = 0;

        if (predicate is null || predicate.Body is null)
        {
            return SqlFragment.AlwaysTrue;
        }

        return Visit(predicate.Body);
    }

    public SqlFragment TranslatePredicate(Expression expression)
    {
        parameterIndex = 0;
        return Visit(expression);
    }

    private SqlFragment Visit(Expression node)
    {
        switch (node)
        {
            case ConstantExpression constant when constant.Type == typeof(bool):
                return (bool)constant.Value! ? SqlFragment.AlwaysTrue : SqlFragment.AlwaysFalse;

            case BinaryExpression binary:
                return VisitBinary(binary);

            case MemberExpression member when member.Type == typeof(bool):
                return VisitBooleanMember(member);

            case UnaryExpression { NodeType: ExpressionType.Not } not:
            {
                var inner = Visit(not.Operand);
                return new SqlFragment { Sql = $"NOT ({inner.Sql})", Parameters = inner.Parameters };
            }

            case MethodCallExpression call:
                return VisitMethodCall(call);

            default:
                throw new SqliteTranslationException($"Unsupported expression node '{node.NodeType}' of type '{node.Type.Name}'.");
        }
    }

    private SqlFragment VisitBinary(BinaryExpression node)
    {
        switch (node.NodeType)
        {
            case ExpressionType.AndAlso:
            case ExpressionType.And:
                return SqlFragment.Combine(Visit(node.Left), Visit(node.Right), "AND");

            case ExpressionType.OrElse:
            case ExpressionType.Or:
                return SqlFragment.Combine(Visit(node.Left), Visit(node.Right), "OR");

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                if (TryTranslateReferenceComparison(node, out var reference))
                {
                    return reference;
                }

                return VisitComparison(node, MapOperator(node.NodeType));

            default:
                throw new SqliteTranslationException($"Unsupported binary operator '{node.NodeType}'.");
        }
    }

    private SqlFragment VisitComparison(BinaryExpression node, string op)
    {
        var left = SqliteJsonPathResolver.Unwrap(node.Left);
        var right = SqliteJsonPathResolver.Unwrap(node.Right);

        if (SqliteJsonPathResolver.TryResolve(left, out var leftPath, out var leftIsColumn) && TryEvaluate(right, out var rightValue))
        {
            return BuildComparison(leftPath, leftIsColumn, op, rightValue);
        }

        if (SqliteJsonPathResolver.TryResolve(right, out var rightPath, out var rightIsColumn) && TryEvaluate(left, out var leftValue))
        {
            return BuildComparison(rightPath, rightIsColumn, op, leftValue);
        }

        throw new SqliteTranslationException($"Cannot translate comparison between '{left}' and '{right}'.");
    }

    private bool TryTranslateReferenceComparison(BinaryExpression node, out SqlFragment fragment)
    {
        fragment = null!;

        if (node.Method is null || !IsReferenceOperator(node.Method))
        {
            return false;
        }

        var left = SqliteJsonPathResolver.Unwrap(node.Left);
        var right = SqliteJsonPathResolver.Unwrap(node.Right);
        var op = MapOperator(node.NodeType);

        if (SqliteJsonPathResolver.TryResolve(left, out var leftPath, out var leftIsColumn) && TryResolveReferenceValue(right, out var rightValue))
        {
            fragment = BuildComparison(leftPath, leftIsColumn, op, rightValue);
            return true;
        }

        if (SqliteJsonPathResolver.TryResolve(right, out var rightPath, out var rightIsColumn) && TryResolveReferenceValue(left, out var leftValue))
        {
            fragment = BuildComparison(rightPath, rightIsColumn, op, leftValue);
            return true;
        }

        return false;
    }

    private static bool IsReferenceOperator(MethodInfo method)
    {
        if (!method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
            return false;
        }

        var declaring = method.DeclaringType;

        if (declaring is null)
        {
            return false;
        }

        if (declaring == typeof(GoLive.Saturn.Data.Entities.WeakRef))
        {
            return true;
        }

        return declaring.IsGenericType
               && (declaring.GetGenericTypeDefinition() == typeof(GoLive.Saturn.Data.Entities.Ref<>)
                   || declaring.GetGenericTypeDefinition() == typeof(GoLive.Saturn.Data.Entities.WeakRef<>));
    }

    private static bool TryResolveReferenceValue(Expression expression, out object? value)
    {
        value = null;

        if (!TryEvaluate(expression, out var evaluated))
        {
            return false;
        }

        switch (evaluated)
        {
            case null:
                value = null;
                return true;
            case string text:
                value = text;
                return true;
            case GoLive.Saturn.Data.Entities.WeakRef weak:
                value = weak.Id;
                return true;
        }

        var idProperty = evaluated.GetType().GetProperty("Id");

        if (idProperty is null)
        {
            return false;
        }

        value = idProperty.GetValue(evaluated) as string;
        return true;
    }

    private SqlFragment VisitBooleanMember(MemberExpression member)
    {
        if (!SqliteJsonPathResolver.TryResolve(member, out var path, out var isColumn))
        {
            throw new SqliteTranslationException($"Cannot resolve boolean member '{member}'.");
        }

        var operand = isColumn ? path : JsonExtract(path);
        return new SqlFragment { Sql = $"{operand} = 1" };
    }

    private SqlFragment VisitMethodCall(MethodCallExpression node)
    {
        if (TryTranslateStringMethod(node, out var stringMethod))
        {
            return stringMethod;
        }

        if (TryTranslateCollectionContains(node, out var collectionContains))
        {
            return collectionContains;
        }

        if (TryTranslateStaticContains(node, out var staticContains))
        {
            return staticContains;
        }

        throw new SqliteTranslationException($"Unsupported method call '{node.Method.Name}'.");
    }

    private bool TryTranslateStringMethod(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null!;

        if (node.Object is null || node.Object.Type != typeof(string))
        {
            return false;
        }

        var target = SqliteJsonPathResolver.Unwrap(node.Object);

        if (!SqliteJsonPathResolver.TryResolve(target, out var path, out var isColumn))
        {
            return false;
        }

        var operand = isColumn ? path : JsonExtract(path);

        switch (node.Method.Name)
        {
            case nameof(string.Contains) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var containsValue):
            {
                var parameter = CreateParameter(containsValue);
                fragment = new SqlFragment { Sql = $"instr({operand}, {parameter.ParameterName}) > 0", Parameters = new[] { parameter } };
                return true;
            }

            case nameof(string.StartsWith) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var startsWithValue):
            {
                var parameter = CreateParameter(EscapeLike(Convert.ToString(startsWithValue, CultureInfo.InvariantCulture)) + "%");
                fragment = new SqlFragment { Sql = $"{operand} LIKE {parameter.ParameterName} ESCAPE '\\'", Parameters = new[] { parameter } };
                return true;
            }

            case nameof(string.EndsWith) when node.Arguments.Count == 1 && TryEvaluate(node.Arguments[0], out var endsWithValue):
            {
                var parameter = CreateParameter("%" + EscapeLike(Convert.ToString(endsWithValue, CultureInfo.InvariantCulture)));
                fragment = new SqlFragment { Sql = $"{operand} LIKE {parameter.ParameterName} ESCAPE '\\'", Parameters = new[] { parameter } };
                return true;
            }

            default:
                return false;
        }
    }

    private bool TryTranslateCollectionContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null!;

        if (node.Object is null || node.Arguments.Count != 1 || node.Method.Name != nameof(IList.Contains))
        {
            return false;
        }

        var target = SqliteJsonPathResolver.Unwrap(node.Object);

        if (target.Type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(target.Type))
        {
            return false;
        }

        if (!SqliteJsonPathResolver.TryResolve(target, out var path, out _) || !TryEvaluate(node.Arguments[0], out var value))
        {
            return false;
        }

        var parameter = CreateParameter(value);
        fragment = new SqlFragment
        {
            Sql = $"EXISTS (SELECT 1 FROM json_each(_doc, '{path}') WHERE value = {parameter.ParameterName})",
            Parameters = new[] { parameter }
        };
        return true;
    }

    private bool TryTranslateStaticContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null!;

        Expression collectionExpression;
        Expression itemExpression;

        if (node.Object is not null && node.Arguments.Count == 1)
        {
            collectionExpression = node.Object;
            itemExpression = node.Arguments[0];
        }
        else if (node.Object is null && node.Arguments.Count == 2)
        {
            collectionExpression = node.Arguments[0];
            itemExpression = node.Arguments[1];
        }
        else
        {
            return false;
        }

        if (collectionExpression.Type == typeof(string))
        {
            return false;
        }

        if (!TryEvaluateCollection(collectionExpression, out var collection))
        {
            return false;
        }

        var item = SqliteJsonPathResolver.Unwrap(itemExpression);

        if (!SqliteJsonPathResolver.TryResolve(item, out var path, out var isColumn))
        {
            return false;
        }

        var values = collection.Cast<object>().ToList();

        if (values.Count == 0)
        {
            fragment = SqlFragment.AlwaysFalse;
            return true;
        }

        var parameters = values.Select(CreateParameter).ToList();
        var names = string.Join(", ", parameters.Select(parameter => parameter.ParameterName));
        var operand = isColumn ? path : JsonExtract(path);

        fragment = new SqlFragment { Sql = $"{operand} IN ({names})", Parameters = parameters };
        return true;
    }

    private SqlFragment BuildComparison(string path, bool isColumn, string op, object? value)
    {
        var operand = isColumn ? path : JsonExtract(path);

        if (value is null)
        {
            return op switch
            {
                "=" => new SqlFragment { Sql = $"{operand} IS NULL" },
                "<>" => new SqlFragment { Sql = $"{operand} IS NOT NULL" },
                _ => throw new SqliteTranslationException($"Cannot apply operator '{op}' to null.")
            };
        }

        var parameter = CreateParameter(value);
        return new SqlFragment { Sql = $"{operand} {op} {parameter.ParameterName}", Parameters = new[] { parameter } };
    }

    private SqliteParameter CreateParameter(object? value)
        => new($"@p{parameterIndex++}", value ?? DBNull.Value);

    private static string MapOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => "=",
        ExpressionType.NotEqual => "<>",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        _ => throw new SqliteTranslationException($"Unsupported operator '{nodeType}'.")
    };

    private static string JsonExtract(string path) => $"json_extract(_doc, '{path}')";

    private static string EscapeLike(string? value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static bool TryEvaluateCollection(Expression expression, out IEnumerable collection)
    {
        collection = null!;

        var node = SqliteJsonPathResolver.Unwrap(expression);

        while (node.Type.IsByRefLike)
        {
            switch (node)
            {
                case NewExpression { Arguments.Count: >= 1 } newExpression:
                    node = SqliteJsonPathResolver.Unwrap(newExpression.Arguments[0]);
                    break;
                case MethodCallExpression { Arguments.Count: >= 1 } call:
                    node = SqliteJsonPathResolver.Unwrap(call.Arguments[0]);
                    break;
                default:
                    return false;
            }
        }

        if (!TryEvaluate(node, out var value) || value is not IEnumerable enumerable)
        {
            return false;
        }

        collection = enumerable;
        return true;
    }

    private static bool TryEvaluate(Expression expression, out object? value)
    {
        var node = SqliteJsonPathResolver.Unwrap(expression);

        if (node.Type.IsByRefLike)
        {
            value = null;
            return false;
        }

        switch (node)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case MemberExpression { Expression: ConstantExpression owner } member:
                value = member.Member switch
                {
                    FieldInfo field => field.GetValue(owner.Value),
                    PropertyInfo property => property.GetValue(owner.Value),
                    _ => null
                };
                return true;
        }

        if (!ReferencesParameter(node))
        {
            var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(node, typeof(object)));
            value = lambda.Compile(preferInterpretation: true)();
            return true;
        }

        value = null;
        return false;
    }

    private static bool ReferencesParameter(Expression expression)
        => new ParameterFinder().Find(expression);

    private sealed class ParameterFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public bool Find(Expression expression)
        {
            Visit(expression);
            return Found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found = true;
            return base.VisitParameter(node);
        }
    }
}
