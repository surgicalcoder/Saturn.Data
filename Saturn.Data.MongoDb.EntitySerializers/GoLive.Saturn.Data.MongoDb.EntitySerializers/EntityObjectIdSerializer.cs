using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class EntityObjectIdSerializer : SerializerBase<ObjectId>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ObjectId value)
        {
            if (value == default || value == ObjectId.Empty)
            {
                value = ObjectId.GenerateNewId();
            }

            base.Serialize(context, args, value);
        }
    }
}