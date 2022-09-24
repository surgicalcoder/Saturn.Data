using System;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class EncryptedStringSerializer : SerializerBase<EncryptedString>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, EncryptedString value)
        {
            if (value == null || (( string.IsNullOrEmpty(value.Encoded) && string.IsNullOrEmpty(value.Hash) && string.IsNullOrEmpty(value.Salt)) || string.IsNullOrEmpty(value.Decoded)))
            {
                context.Writer.WriteNull();
            }
            
            if (string.IsNullOrEmpty(value.Salt))
            {
                value.Salt = Crypto.Random.GetRandomString(32);
            }
            
            if (!string.IsNullOrWhiteSpace(value.Decoded))
            {
                value.Encoded = Crypto.Encryption.EncryptStringAES(value.Decoded, Crypto.CryptoSingleton.Instance.MasterEncryptionKey, value.Salt);
                value.Hash = Crypto.Hash.CalculateSHA512(value.Decoded);
            }

            context.Writer.WriteStartDocument();
            context.Writer.WriteString("Encoded", value.Encoded);
            context.Writer.WriteString("Hash", value.Hash);
            context.Writer.WriteString("Salt", value.Salt);
            context.Writer.WriteEndDocument();
        }

        public override EncryptedString Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
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
            
            var item = new EncryptedString
            {
                Encoded = context.Reader.ReadString(),
                Hash = context.Reader.ReadString("Hash"),
                Salt = context.Reader.ReadString("Salt")
            };
            
            context.Reader.ReadEndDocument();

            if (!string.IsNullOrWhiteSpace(item.Encoded) && !string.IsNullOrWhiteSpace(item.Salt))
            {
                item.Decoded = Crypto.Encryption.DecryptStringAES(item.Encoded, Crypto.CryptoSingleton.Instance.MasterEncryptionKey, item.Salt);
                item.Populated = true;
            }
            
            return item;
        }
    }
}
