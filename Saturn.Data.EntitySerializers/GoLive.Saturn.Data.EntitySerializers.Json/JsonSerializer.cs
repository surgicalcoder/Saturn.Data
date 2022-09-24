using System.Dynamic;
using System.Text.Json;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers.Json
{
    public class JsonSerializer : SerializerBase<JsonElement>
    {
        ExpandoObjectSerializer seri = new ExpandoObjectSerializer();
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonElement value)
        {
            seri.Serialize(context, value.ToObject<ExpandoObject>());
        }
    }
}
