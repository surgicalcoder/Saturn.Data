using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers;

public class WeakRefSerializer : SerializerBase<WeakRef>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, WeakRef value)
    {
        if (value?.Id == null)
        {
            context.Writer.WriteNull();
        }
        else
        {
            if (ObjectId.TryParse(value.Id, out var objectId))
            {
                context.Writer.WriteObjectId(objectId);
            }
            else
            {
                context.Writer.WriteString(value.Id);
            }
        }
    }

    public override WeakRef Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
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

        if (context.Reader.CurrentBsonType == BsonType.ObjectId)
        {
            return new WeakRef(context.Reader.ReadObjectId().ToString());
        }

        if (context.Reader.CurrentBsonType == BsonType.String)
        {
            return new WeakRef(context.Reader.ReadString());
        }

        if (context.Reader.CurrentBsonType == BsonType.Document)
        {
            context.Reader.ReadStartDocument();

            string id = null;
            if (context.Reader.CurrentBsonType == BsonType.ObjectId)
            {
                id = context.Reader.ReadObjectId().ToString();
            }
            else if (context.Reader.CurrentBsonType == BsonType.String)
            {
                id = context.Reader.ReadString();
            }

            context.Reader.ReadEndDocument();
            return id != null ? new WeakRef(id) : null;
        }

        return null;
    }
}
    
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

            return new WeakRef<T>(context.Reader.ReadString());
        }

        if (context.Reader.CurrentBsonType == BsonType.Document)
        {
            context.Reader.ReadStartDocument();

            string id = null;
            if (context.Reader.CurrentBsonType == BsonType.String)
            {
                id = context.Reader.ReadString();
            }

            context.Reader.ReadEndDocument();
            return id != null ? new WeakRef<T>(id) : null;
        }
        else
        {
            if (context.Reader.State == BsonReaderState.Value)
            {

                try
                {
                    return new WeakRef<T>(context.Reader.ReadString());
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}