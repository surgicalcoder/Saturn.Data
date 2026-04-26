using System;
using System.Linq.Expressions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public static class ScopeModelHelper
{
    public static void SetScope<TItem>(TItem entity, string scopeId)
        where TItem : IScopedById
    {
        if (entity == null)
        {
            return;
        }

        if (!TrySetReferenceScope(entity, "Scope", scopeId))
        {
            entity.ScopeId = scopeId;
        }
    }

    public static void SetSecondScope<TItem>(TItem entity, string secondScopeId)
        where TItem : ISecondScopedById
    {
        if (entity == null)
        {
            return;
        }

        if (!TrySetReferenceScope(entity, "SecondScope", secondScopeId))
        {
            entity.SecondScopeId = secondScopeId;
        }
    }

    public static Expression<Func<TItem, bool>> BuildScopePredicate<TItem>(string scopeId)
        where TItem : IScopedById
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var body = BuildScopeBody(parameter, "Scope", nameof(IScopedById.ScopeId), scopeId);

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    public static Expression<Func<TItem, bool>> BuildSecondScopePredicate<TItem>(string secondScopeId)
        where TItem : ISecondScopedById
    {
        var parameter = Expression.Parameter(typeof(TItem), "item");
        var body = BuildScopeBody(parameter, "SecondScope", nameof(ISecondScopedById.SecondScopeId), secondScopeId);

        return Expression.Lambda<Func<TItem, bool>>(body, parameter);
    }

    private static Expression BuildScopeBody(ParameterExpression parameter, string referencePropertyName, string idPropertyName, string scopeId)
    {
        var referenceProperty = parameter.Type.GetProperty(referencePropertyName);

        if (referenceProperty?.CanRead == true)
        {
            var member = Expression.Property(parameter, referenceProperty);
            var constant = Expression.Constant(scopeId, typeof(string));

            if (member.Type == typeof(string))
            {
                return Expression.Equal(member, constant);
            }

            try
            {
                var converted = Expression.Convert(constant, member.Type);
                return Expression.Equal(member, converted);
            }
            catch (InvalidOperationException)
            {
                // Fall back to *ScopeId if no conversion exists.
            }
        }

        var idProperty = parameter.Type.GetProperty(idPropertyName);

        if (idProperty?.CanRead == true)
        {
            var member = Expression.Property(parameter, idProperty);
            var constant = Expression.Constant(scopeId, member.Type);
            return Expression.Equal(member, constant);
        }

        throw new InvalidOperationException($"Unable to build scope predicate for type '{parameter.Type.FullName}'.");
    }

    private static bool TrySetReferenceScope<TItem>(TItem entity, string propertyName, string scopeId)
    {
        var property = entity.GetType().GetProperty(propertyName);

        if (property?.CanWrite != true)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            property.SetValue(entity, null);
            return true;
        }

        var propertyType = property.PropertyType;

        if (propertyType == typeof(string))
        {
            property.SetValue(entity, scopeId);
            return true;
        }

        if (propertyType == typeof(WeakRef))
        {
            property.SetValue(entity, new WeakRef(scopeId));
            return true;
        }

        if (propertyType.IsGenericType)
        {
            var genericDefinition = propertyType.GetGenericTypeDefinition();

            if (genericDefinition == typeof(Ref<>) || genericDefinition == typeof(WeakRef<>))
            {
                var value = Activator.CreateInstance(propertyType, scopeId);
                property.SetValue(entity, value);
                return true;
            }
        }

        return false;
    }
}
