using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions;

public class RepositoryOptions
{
    public RepositoryOptions()
    {
        GetCollectionName = delegate(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(WrappedEntity<>))
            {
                return $"{WrappedEntityPrefix}{type.GenericTypeArguments[0].Name}{WrappedEntityPostfix}";
            }
            return type.Name;
        };
    }

    public string WrappedEntityPrefix { get; set; } = "w_";
    public string WrappedEntityPostfix { get; set; }

    public Func<Type, string> GetCollectionName { get; set; }
        
    public Func<Type, string> TransparentScopeProvider { get; set; }
}