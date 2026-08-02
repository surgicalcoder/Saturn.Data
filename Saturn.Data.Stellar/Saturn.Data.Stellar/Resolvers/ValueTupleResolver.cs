using MessagePack;
using MessagePack.Formatters;

namespace Saturn.Data.Stellar.Resolvers;

internal class GenericValueTupleFormatter<T1, T2> : IMessagePackFormatter<(T1, T2)>
{
    public void Serialize(ref MessagePackWriter writer, (T1, T2) value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        options.Resolver.GetFormatterWithVerify<T1>().Serialize(ref writer, value.Item1, options);
        options.Resolver.GetFormatterWithVerify<T2>().Serialize(ref writer, value.Item2, options);
    }

    public (T1, T2) Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        _ = reader.ReadArrayHeader();
        var item1 = options.Resolver.GetFormatterWithVerify<T1>().Deserialize(ref reader, options);
        var item2 = options.Resolver.GetFormatterWithVerify<T2>().Deserialize(ref reader, options);
        return (item1, item2);
    }
}

public class ValueTupleResolver : IFormatterResolver
{
    public static readonly ValueTupleResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        var type = typeof(T);

        if (type.IsGenericType && type.Name.StartsWith("ValueTuple"))
        {
            var args = type.GenericTypeArguments;

            var formatterType = args.Length switch
            {
                2 => typeof(GenericValueTupleFormatter<,>).MakeGenericType(args),
                _ => null
            };

            if (formatterType is not null)
            {
                return (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType);
            }
        }

        return null;
    }
}
