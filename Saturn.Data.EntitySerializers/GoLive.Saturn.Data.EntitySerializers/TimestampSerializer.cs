using System;
using GoLive.Saturn.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers
{
    public class TimestampSerializer : ClassSerializerBase<Timestamp>
    {
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override Timestamp Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
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
            
            var ts = new Timestamp();

            context.Reader.ReadStartDocument();

            var CreatedDate = context.Reader.ReadDateTime("CreatedDate");
            var LastModifiedDate = context.Reader.ReadDateTime("LastModifiedDate");

            context.Reader.ReadEndDocument();

            ts.CreatedDate = epoch.AddMilliseconds(CreatedDate);
            ts.LastModifiedDate = epoch.AddMilliseconds(LastModifiedDate);

            return ts;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Timestamp value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
                return;
            }

            if (!value.BypassAutomaticDatePopulation)
            {
                if (!value.CreatedDate.HasValue)
                {
                    value.CreatedDate = DateTime.UtcNow;
                }

                value.LastModifiedDate = DateTime.UtcNow;
            }
            
            context.Writer.WriteStartDocument();

            context.Writer.WriteName("CreatedDate");

            if (value.CreatedDate.HasValue)
            {
                context.Writer.WriteDateTime(getEpoch(value.CreatedDate.Value));
            }
            else
            {
                context.Writer.WriteNull();
            }


            context.Writer.WriteName("LastModifiedDate");


            if (value.LastModifiedDate.HasValue)
            {
                context.Writer.WriteDateTime(getEpoch(value.LastModifiedDate.Value));
            }
            else
            {
                context.Writer.WriteNull();
            }

            context.Writer.WriteEndDocument();
        }

        long getEpoch(DateTime dateTime)
        {

            return (long)dateTime.ToUniversalTime().Subtract(epoch).TotalMilliseconds;
        }
    }
}