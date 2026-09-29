using System.Linq.Expressions;

namespace Saturn.Data.DocumentDb.Query;

internal static class PredicateComposer
{
    public static Expression<Func<TItem, bool>> AndAlso<TItem>(Expression<Func<TItem, bool>> left, Expression<Func<TItem, bool>> right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        var body = Expression.AndAlso(left.Body, rightBody);

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression source;
        private readonly ParameterExpression target;

        public ParameterReplacer(ParameterExpression source, ParameterExpression target)
        {
            this.source = source;
            this.target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }
}
