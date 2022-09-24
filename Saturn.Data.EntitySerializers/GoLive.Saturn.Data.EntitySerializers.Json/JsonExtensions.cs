using System;
using System.Buffers;
using System.Text.Json;

namespace GoLive.Saturn.Data.EntitySerializers.Json
{
    internal static partial class JsonExtensions
    {
        public static T ToObject<T>(this JsonElement element)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();

            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                element.WriteTo(writer);
            }

            return Utf8Json.JsonSerializer.Deserialize<T>(bufferWriter.WrittenMemory.ToArray()); // This is needed because ATM, System.Text.Json does not support ExpandoObjects.
        }

        public static T ToObject<T>(this JsonDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return document.RootElement.ToObject<T>();
        }
    }
}