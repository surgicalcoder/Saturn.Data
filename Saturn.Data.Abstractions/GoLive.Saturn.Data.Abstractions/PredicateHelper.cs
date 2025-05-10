using System;
using System.Linq;
using System.Linq.Expressions;

namespace GoLive.Saturn.Data.Abstractions;

public static class PredicateHelper
{
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> exp, Expression<Func<T, bool>> newExp)
    {
        // Ensure both expressions have the same parameter type
        if (exp.Parameters.Count != newExp.Parameters.Count)
            throw new ArgumentException("Expressions must have the same number of parameters.");

        // Reuse the parameter from the first expression
        var parameter = exp.Parameters.First();
        var visitor = new ParameterUpdateVisitor(newExp.Parameters.First(), parameter);
        var newBody = visitor.Visit(newExp.Body);

        // Combine the expressions directly
        var binExp = Expression.And(exp.Body, newBody);
        return Expression.Lambda<Func<T, bool>>(binExp, parameter);
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> exp, Expression<Func<T, bool>> newExp)
    {
        // Ensure both expressions have the same parameter type
        if (exp.Parameters.Count != newExp.Parameters.Count)
            throw new ArgumentException("Expressions must have the same number of parameters.");

        // Reuse the parameter from the first expression
        var parameter = exp.Parameters.First();
        var visitor = new ParameterUpdateVisitor(newExp.Parameters.First(), parameter);
        var newBody = visitor.Visit(newExp.Body);

        // Combine the expressions directly
        var binExp = Expression.Or(exp.Body, newBody);
        return Expression.Lambda<Func<T, bool>>(binExp, parameter);
    }

    public class ParameterUpdateVisitor : System.Linq.Expressions.ExpressionVisitor
    {
        private ParameterExpression _oldParameter;
        private ParameterExpression _newParameter;

        public ParameterUpdateVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return object.ReferenceEquals(node, _oldParameter) ? _newParameter : base.VisitParameter(node);
        }
    }
}