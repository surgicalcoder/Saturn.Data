using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Sqlite.Query;

public static class SqliteJsonPathResolver
{
    public static bool TryResolve(Expression expression, out string path, out bool isColumn)
    {
        path = null!;
        isColumn = false;

        var current = Unwrap(expression);

        if (current is not MemberExpression member)
        {
            return false;
        }

        var segments = new List<string>();
        Expression cursor = member;

        while (cursor is MemberExpression segment)
        {
            segments.Insert(0, segment.Member.Name);
            cursor = Unwrap(segment.Expression!);
        }

        if (cursor is not ParameterExpression)
        {
            return false;
        }

        if (segments.Count >= 2 && segments[^1] == nameof(Entity.Id) && IsReferenceType(member.Expression?.Type))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        if (segments.Count == 1 && segments[0] == nameof(Entity.Id))
        {
            path = "_id";
            isColumn = true;
            return true;
        }

        if (segments.Count == 1 && segments[0] == nameof(Entity.Version))
        {
            path = "_v";
            isColumn = true;
            return true;
        }

        path = "$." + string.Join(".", segments);
        return true;
    }

    public static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    public static bool IsReferenceType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type == typeof(WeakRef))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(Ref<>) || definition == typeof(WeakRef<>);
    }
}
