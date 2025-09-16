using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

namespace Saturn.Data.Stellar.Resolvers;

public class CryptoStringResolver : IFormatterResolver
{
    public static readonly CryptoStringResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        if (typeof(T) == typeof(EncryptedString))
        {
            return (IMessagePackFormatter<T>)(object)new EncryptedStringFormatter();
        }
            
        if (typeof(T) == typeof(HashedString))
        {
            return (IMessagePackFormatter<T>)(object)new HashedStringFormatter();
        }

        // Handle Ref<T> types
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Ref<>))
        {
            var entityType = typeof(T).GetGenericArguments()[0];
            var formatterType = typeof(RefFormatter<>).MakeGenericType(entityType);
            return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType);
        }

        // Handle WeakRef<T> types
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(WeakRef<>))
        {
            var entityType = typeof(T).GetGenericArguments()[0];
            var formatterType = typeof(WeakRefFormatter<>).MakeGenericType(entityType);
            return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType);
        }
            
        return null;
    }
}