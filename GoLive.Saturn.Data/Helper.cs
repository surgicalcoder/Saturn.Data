using System;
using System.Linq;
using System.Linq.Expressions;

namespace GoLive.Saturn.Data
{
    public static class Helper
    {
        public static string GetPropertyName<TModel, TValue>(this Expression<Func<TModel, TValue>> propertySelector, char delimiter = '.', char endTrim = ')')
        {
            var asString = propertySelector.ToString(); // gives you: "o => o.Whatever"
            var firstDelim = asString.IndexOf(delimiter); // make sure there is a beginning property indicator; the "." in "o.Whatever" -- this may not be necessary?

            return firstDelim < 0
                ? asString
                : asString.Substring(firstDelim + 1).TrimEnd(endTrim);
        }
        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> exp, Expression<Func<T, bool>> newExp)
        {
            // get the visitor
            var visitor = new ParameterUpdateVisitor(newExp.Parameters.First(), exp.Parameters.First());
            // replace the parameter in the expression just created
            newExp = visitor.Visit(newExp) as Expression<Func<T, bool>>;

            // now you can and together the two expressions
            var binExp = Expression.And(exp.Body, newExp.Body);
            // and return a new lambda, that will do what you want. NOTE that the binExp has reference only to te newExp.Parameters[0] (there is only 1) parameter, and no other
            return Expression.Lambda<Func<T, bool>>(binExp, newExp.Parameters);
        }

        class ParameterUpdateVisitor : System.Linq.Expressions.ExpressionVisitor
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
                if (object.ReferenceEquals(node, _oldParameter))
                    return _newParameter;

                return base.VisitParameter(node);
            }
        }

        //public static Expression<Func<T, bool>> Combine<T>(this Expression<Func<T, bool>> predicate, Func<Expression, Expression, BinaryExpression> combination, Expression<Func<T, bool>> withPredicate)
        //{
        //    var invocation = Expression.Invoke(withPredicate, predicate.Parameters);
        //    var combined = combination(predicate.Body, invocation);
        //    return Expression.Lambda<Func<T, bool>>(combined, predicate.Parameters);
        //}
    }
}