using System;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class HashedStringSerializer : SerializerBase<HashedString>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, HashedString value)
        {
            if (value == null || (string.IsNullOrEmpty(value.Hash) && string.IsNullOrEmpty(value.Salt)) || string.IsNullOrEmpty(value.Decoded))
            {
                context.Writer.WriteNull();
            }
            
            if (String.IsNullOrEmpty(value.Salt))
            {
                value.Salt = Crypto.Random.GetRandomString(32);
            }

            if (!string.IsNullOrWhiteSpace(value.Decoded))
            {
                value.Hash = Crypto.Hash.CalculateSHA512(value.Decoded + value.Salt);
            }
            else if (string.IsNullOrWhiteSpace(value.Hash))
            {
                context.Writer.WriteNull();
                return;
            }

            context.Writer.WriteStartDocument();
            context.Writer.WriteString("Hash", value.Hash);
            context.Writer.WriteString("Salt", value.Salt);
            context.Writer.WriteEndDocument();
        }

        public override HashedString Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
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

            context.Reader.ReadStartDocument();
            
            var item = new HashedString
            {
                Hash = context.Reader.ReadString("Hash"),
                Salt = context.Reader.ReadString("Salt")
            };
            
            context.Reader.ReadEndDocument();
            
            if (!string.IsNullOrWhiteSpace(item.Hash) && !string.IsNullOrWhiteSpace(item.Salt))
            {
                item.Populated = true;
            }
            
            return item;
        }
    }
}