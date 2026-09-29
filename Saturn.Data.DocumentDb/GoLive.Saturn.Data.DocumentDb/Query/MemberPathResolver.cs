using System.Linq.Expressions;

namespace Saturn.Data.DocumentDb.Query;

internal static class MemberPathResolver
{
    public static bool TryResolve(LambdaExpression lambda, out string path)
    {
        path = null;

        if (lambda is null)
        {
            return false;
        }

        var body = lambda.Body;

        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        var segments = new List<string>();

        while (body is MemberExpression member)
        {
            segments.Insert(0, member.Member.Name);
            body = member.Expression;
        }

        if (body is not ParameterExpression || segments.Count == 0)
        {
            return false;
        }

        path = string.Join(".", segments);
        return true;
    }
}
