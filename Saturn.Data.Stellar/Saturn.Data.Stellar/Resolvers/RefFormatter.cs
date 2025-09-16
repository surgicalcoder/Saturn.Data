using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

namespace Saturn.Data.Stellar.Resolvers;

public class RefFormatter<T> : IMessagePackFormatter<Ref<T>> where T : Entity, new()
{
    public void Serialize(ref MessagePackWriter writer, Ref<T> value, MessagePackSerializerOptions options)
    {
        if (value == default || value.Id == null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNil();
        }
        else
        {
            // Serialize as just the Id string
            writer.Write(value.Id);
        }
    }

    public Ref<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return default;
        }

        try
        {
            // Read the Id as a string
            var id = reader.ReadString();
                
            if (string.IsNullOrWhiteSpace(id))
            {
                return default;
            }

            return new Ref<T>(id);
        }
        catch
        {
            return default;
        }
    }
}