using System;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class RefSerializer<T> : SerializerBase<Ref<T>>, IBsonDocumentSerializer where T : Entity, new()
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Ref<T> value)
        {
            if (value == default || value.Id == null || string.IsNullOrWhiteSpace(value.Id))
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteObjectId(new ObjectId(value.Id));
            }
        }

        public override Ref<T> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.State == BsonReaderState.Name)
            {
                context.Reader.ReadStartDocument();

                return new Ref<T>(context.Reader.ReadObjectId().ToString());
            }

            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();

                return default;
            }

            if (context.Reader.CurrentBsonType == BsonType.Document)
            {
                context.Reader.ReadStartDocument();

                return context.Reader.ReadBsonType() == BsonType.String ? new Ref<T>(context.Reader.ReadString()) : new Ref<T>(context.Reader.ReadObjectId().ToString());
            }

            if (context.Reader.State == BsonReaderState.Value)
            {

                try
                {
                    return new Ref<T>(context.Reader.ReadObjectId().ToString());
                }
                catch
                {
                    return default;
                }
            }

            try
            {
                return context.Reader.ReadBsonType() == BsonType.String ? new Ref<T>(context.Reader.ReadString()) : new Ref<T>(context.Reader.ReadObjectId().ToString());
            }
            catch (Exception)
            {
                return default;
            }
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            
            if (memberName == "Id")
            {
                var serializer = BsonSerializer.LookupSerializer<ObjectId>();
                serializationInfo = BsonSerializationInfo.CreateWithPath(
                    new []{""},
                    serializer,
                    serializer.ValueType
                );
                return true;
            }

            serializationInfo = null;
            return false;
        }
    }
}