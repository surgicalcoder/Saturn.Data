using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

public class EntityIgnoreFormatter<T> : IMessagePackFormatter<T> where T : Entity
{
    private static readonly HashSet<string> IgnoredProperties = new() { nameof(Entity.Changes), nameof(Entity.EnableChangeTracking) };

    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        var properties = typeof(T).GetProperties()
                                  .Where(p => !IgnoredProperties.Contains(p.Name))
                                  .ToArray();

        writer.WriteMapHeader(properties.Length);

        foreach (var prop in properties)
        {
            writer.Write(prop.Name);
            MessagePackSerializer.Serialize(ref writer, prop.GetValue(value), options);
        }
    }

    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var mapLength = reader.ReadMapHeader();
        var instance = Activator.CreateInstance<T>();

        for (int i = 0; i < mapLength; i++)
        {
            var key = reader.ReadString();
            if (IgnoredProperties.Contains(key))
            {
                reader.Skip();
                continue;
            }

            var prop = typeof(T).GetProperty(key);
            if (prop != null && prop.CanWrite)
            {
                var value = MessagePackSerializer.Deserialize(prop.PropertyType, ref reader, options);
                prop.SetValue(instance, value);
            }
            else
            {
                reader.Skip();
            }
        }

        return instance;
    }
}