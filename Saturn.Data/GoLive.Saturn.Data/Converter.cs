using System;
using System.Linq;
using System.Linq.Expressions;

namespace GoLive.Saturn.Data
{
    internal class Converter<TTo>
    {
        class ConversionVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression newParameter;
            private readonly ParameterExpression oldParameter;

            public ConversionVisitor(ParameterExpression newParameter, ParameterExpression oldParameter)
            {
                this.newParameter = newParameter;
                this.oldParameter = oldParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return newParameter; // replace all old param references with new ones
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression != oldParameter) // if instance is not old parameter - do nothing
                    return base.VisitMember(node);

                var newObj = Visit(node.Expression);
                var newMember = newParameter.Type.GetMember(node.Member.Name).First();
                return Expression.MakeMemberAccess(newObj, newMember);
            }
        }
        
        public static Expression<Func<TTo, TR>> Convert<TFrom, TR>(
            Expression<Func<TFrom, TR>> e
        )
        {
            var oldParameter = e.Parameters[0];
            var newParameter = Expression.Parameter(typeof(TTo), oldParameter.Name);
            var converter = new ConversionVisitor(newParameter, oldParameter);
            var newBody = converter.Visit(e.Body);
            return Expression.Lambda<Func<TTo, TR>>(newBody, newParameter);
        }
    }
}