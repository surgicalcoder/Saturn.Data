using System.Text.Json;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Data.DocumentDb.Serialization;

public sealed class WeakRefJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(WeakRef)
           || (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(WeakRef<>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => typeToConvert == typeof(WeakRef)
            ? new WeakRefJsonConverter()
            : (JsonConverter)Activator.CreateInstance(
                typeof(WeakRefOfJsonConverter<>).MakeGenericType(typeToConvert.GenericTypeArguments[0]))!;
}

public sealed class WeakRefJsonConverter : JsonConverter<WeakRef>
{
    public override WeakRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new WeakRef(value);
    }

    public override void Write(Utf8JsonWriter writer, WeakRef value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}

public sealed class WeakRefOfJsonConverter<T> : JsonConverter<WeakRef<T>> where T : Entity
{
    public override WeakRef<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : new WeakRef<T>(value);
    }

    public override void Write(Utf8JsonWriter writer, WeakRef<T> value, JsonSerializerOptions options)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Id);
    }
}
