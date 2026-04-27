using System;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers;

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
            // Ref<T> is stored as a plain scalar ObjectId, not a sub-document.
            // There is no sub-field for "Id" — the value IS the ref.
            //
            // We use StringSerializer(BsonType.ObjectId) so the value type matches the C# "string" Id
            // property, avoiding a type-mismatch exception from the LINQ provider.
            // The element name "_____" is a sentinel that the RefExpressionRewriter handles by
            // rewriting `ref.Id == x` → `ref == new Ref<T>(x)` BEFORE the driver processes the
            // expression, so this code path is never reached for well-formed queries.
            //
            // If you hit this path without the expression rewriter, the query will target
            // "FieldName._____" which does not exist — wrap your predicate with
            // .NormalizeForRef() or use Builders<T>.Filter.Eq(e => e.RefProp, new Ref<T>(id)).
            var serializer = new MongoDB.Bson.Serialization.Serializers.StringSerializer(BsonType.ObjectId);
            serializationInfo = new BsonSerializationInfo("_____", serializer, typeof(string));
            return true;
        }

        serializationInfo = null;
        return false;
    }
}