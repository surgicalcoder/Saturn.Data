using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.EntitySerializers.Json
{
    public class EncryptedStringJsonConverter : JsonConverter<EncryptedString>
    {
        public override EncryptedString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = new EncryptedString();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return str;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString()?.ToLowerInvariant();
                    reader.Read();

                    if (propertyName == nameof(EncryptedString.Decoded).ToLowerInvariant())
                    {
                        var decoded = reader.GetString();
                        str.Decoded = decoded;
                    }
                    else if (propertyName == nameof(EncryptedString.Populated).ToLowerInvariant())
                    {
                        var val = reader.GetBoolean();
                        str.Populated = val;
                    }
                }
            }
            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, EncryptedString value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteBoolean(nameof(EncryptedString.Populated), value.Populated);
            writer.WriteEndObject();
        }
    }
}