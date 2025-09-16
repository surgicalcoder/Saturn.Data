using MessagePack;
using MessagePack.Formatters;
using System;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Resolvers;

public class WeakRefResolver : IFormatterResolver
{
    public static readonly WeakRefResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        var type = typeof(T);
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(WeakRef<>))
        {
            var entityType = type.GetGenericArguments()[0];
            var formatterType = typeof(WeakRefFormatter<>).MakeGenericType(entityType);
            return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType);
        }
        
        return null;
    }
}
