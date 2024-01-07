using System;
using System.Linq;
using System.Linq.Expressions;

namespace GoLive.Saturn.Data.Abstractions;

public static class PredicateHelper
{
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> exp, Expression<Func<T, bool>> newExp)
    {
        var visitor = new ParameterUpdateVisitor(newExp.Parameters.First(), exp.Parameters.First());
        newExp = visitor.Visit(newExp) as Expression<Func<T, bool>>;
        var binExp = Expression.And(exp.Body, newExp.Body);
        return Expression.Lambda<Func<T, bool>>(binExp, newExp.Parameters);
    }        
        
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> exp, Expression<Func<T, bool>> newExp)
    {
        var visitor = new ParameterUpdateVisitor(newExp.Parameters.First(), exp.Parameters.First());
        newExp = visitor.Visit(newExp) as Expression<Func<T, bool>>;
        var binExp = Expression.Or(exp.Body, newExp.Body);
        return Expression.Lambda<Func<T, bool>>(binExp, newExp.Parameters);
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