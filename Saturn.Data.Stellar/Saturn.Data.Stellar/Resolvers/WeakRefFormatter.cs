using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

namespace Saturn.Data.Stellar.Resolvers;

public class WeakRefFormatter<T> : IMessagePackFormatter<WeakRef<T>> where T : Entity
{
    public void Serialize(ref MessagePackWriter writer, WeakRef<T> value, MessagePackSerializerOptions options)
    {
        if (value?.Id == null)
        {
            writer.WriteNil();
        }
        else
        {
            // Serialize as just the Id string
            writer.Write(value.Id);
        }
    }

    public WeakRef<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        try
        {
            // Read the Id as a string
            var id = reader.ReadString();
                
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return new WeakRef<T>(id);
        }
        catch
        {
            return null;
        }
    }
}