using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class WeakRefSerializer<T> : SerializerBase<WeakRef<T>> where T : Entity
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, WeakRef<T> value)
        {
            if (value?.Id == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(value.Id);
            }
        }

        public override WeakRef<T> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.IsAtEndOfFile())
            {
                return null;
            }

            if (context.Reader.CurrentBsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            if (context.Reader.State == BsonReaderState.Name)
            {
                context.Reader.ReadStartDocument();

                return new WeakRef<T>(context.Reader.ReadString().ToString());
            }

            if (context.Reader.CurrentBsonType == BsonType.Document)
            {
                context.Reader.ReadStartDocument();

                return context.Reader.ReadBsonType() == BsonType.String ? new WeakRef<T>(context.Reader.ReadString()) : new WeakRef<T>(context.Reader.ReadString());
            }
            else
            {
                if (context.Reader.State == BsonReaderState.Value)
                {

                    try
                    {
                        return new WeakRef<T>(context.Reader.ReadString().ToString());
                    }
                    catch
                    {
                        return null;
                    }
                }

                return context.Reader.ReadBsonType() == BsonType.String ? new WeakRef<T>(context.Reader.ReadString()) : new WeakRef<T>(context.Reader.ReadString());
            }
        }
    }
}