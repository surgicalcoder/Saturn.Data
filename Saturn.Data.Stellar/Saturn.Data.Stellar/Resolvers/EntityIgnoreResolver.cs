using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

public class EntityIgnoreResolver : IFormatterResolver
{
    public static readonly EntityIgnoreResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        if (typeof(T).IsSubclassOf(typeof(Entity)))
        {
            return (IMessagePackFormatter<T>)Activator.CreateInstance(
                typeof(EntityIgnoreFormatter<>).MakeGenericType(typeof(T)));
        }
        return null;
    }
}