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

            var memory = bufferWriter.WrittenMemory;
            return System.Text.Json.JsonSerializer.Deserialize<T>(memory.Span);
        }

        public static T ToObject<T>(this JsonDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return document.RootElement.ToObject<T>();
        }
    }
}