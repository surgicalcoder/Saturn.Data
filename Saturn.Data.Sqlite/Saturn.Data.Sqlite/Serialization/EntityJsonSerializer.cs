using System.Text.Json;
using System.Text.Json.Serialization;

namespace Saturn.Data.Sqlite.Serialization;

public sealed class EntityJsonSerializer
{
    private readonly JsonSerializerOptions options;

    public EntityJsonSerializer(EntityJsonSerializerOptions? serializerOptions = null)
    {
        serializerOptions ??= new EntityJsonSerializerOptions();

        options = new JsonSerializerOptions
        {
            WriteIndented = serializerOptions.WriteIndented,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = EntityJsonTypeInfoResolver.Create()
        };

        options.Converters.Add(new RefJsonConverterFactory());
        options.Converters.Add(new WeakRefJsonConverterFactory());
        options.Converters.Add(new PropertiesJsonConverter());

        if (serializerOptions.EnumAsString)
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }

    public JsonSerializerOptions JsonOptions => options;

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, options);

    public string Serialize(object value, Type type) => JsonSerializer.Serialize(value, type, options);

    public T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, options)!;

    public object Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type, options)!;
}
