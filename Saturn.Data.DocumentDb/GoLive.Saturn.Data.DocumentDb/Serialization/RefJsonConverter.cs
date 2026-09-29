using System.Text.Json;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb.Serialization;

public sealed class RefJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Ref<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(RefJsonConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]))!;
}

public sealed class RefJsonConverter<T> : JsonConverter<Ref<T>> where T : Entity, new()
{
    public override Ref<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new Ref<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, Ref<T> value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}
