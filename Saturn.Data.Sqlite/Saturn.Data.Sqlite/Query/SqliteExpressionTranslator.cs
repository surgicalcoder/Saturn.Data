using System.Collections;
using System.Linq.Expressions;
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
                return VisitComparison(node, "=");

            case ExpressionType.NotEqual:
                return VisitComparison(node, "<>");

            case ExpressionType.GreaterThan:
                return VisitComparison(node, ">");

            case ExpressionType.GreaterThanOrEqual:
                return VisitComparison(node, ">=");

            case ExpressionType.LessThan:
                return VisitComparison(node, "<");

            case ExpressionType.LessThanOrEqual:
                return VisitComparison(node, "<=");

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

    private SqlFragment BuildComparison(string path, bool isColumn, string op, object? value)
    {
        var operand = isColumn ? path : JsonExtract(path);
        var parameter = CreateParameter(value);
        return new SqlFragment { Sql = $"{operand} {op} {parameter.ParameterName}", Parameters = new[] { parameter } };
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
        if (TryTranslateContains(node, out var contains))
        {
            return contains;
        }

        throw new SqliteTranslationException($"Unsupported method call '{node.Method.Name}'.");
    }

    private bool TryTranslateContains(MethodCallExpression node, out SqlFragment fragment)
    {
        fragment = null!;

        if (node.Method.Name != nameof(Enumerable.Contains))
        {
            return false;
        }

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

        if (!TryEvaluate(collectionExpression, out var collectionValue) || collectionValue is not IEnumerable collection)
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

    private SqliteParameter CreateParameter(object? value)
        => new($"@p{parameterIndex++}", value ?? DBNull.Value);

    private static string JsonExtract(string path) => $"json_extract(_doc, '{path}')";

    private static bool TryEvaluate(Expression expression, out object? value)
    {
        if (expression is ConstantExpression constant)
        {
            value = constant.Value;
            return true;
        }

        if (!ReferencesParameter(expression))
        {
            value = Expression.Lambda(expression).Compile().DynamicInvoke();
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
