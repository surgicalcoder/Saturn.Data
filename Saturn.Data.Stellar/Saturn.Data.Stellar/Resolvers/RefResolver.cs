using MessagePack;
using MessagePack.Formatters;
using System;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.Stellar.Resolvers;

public class RefResolver : IFormatterResolver
{
    public static readonly RefResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        var type = typeof(T);
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>))
        {
            var entityType = type.GetGenericArguments()[0];
            var formatterType = typeof(RefFormatter<>).MakeGenericType(entityType);
            return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType);
        }
        
        return null;
    }
}
