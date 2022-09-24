using System.Dynamic;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GoLive.Saturn.Data.EntitySerializers.Newtonsoft
{
    public class JObjectSerializer : SerializerBase<JObject>
    {
        ExpandoObjectSerializer seri = new ExpandoObjectSerializer();

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JObject value)
        {
            var serializeObject = JsonConvert.SerializeObject(value);
            var deserializeObject = JsonConvert.DeserializeObject<ExpandoObject>(serializeObject, new ExpandoObjectConverter());
            seri.Serialize(context, deserializeObject);
        }
    }
}