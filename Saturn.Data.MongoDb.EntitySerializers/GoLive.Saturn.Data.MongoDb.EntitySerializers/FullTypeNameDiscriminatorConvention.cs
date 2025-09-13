using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace GoLive.Saturn.Data.EntitySerializers;

public class FullTypeNameDiscriminatorConvention : IDiscriminatorConvention
{
    public string ElementName => "_t";

    public Type GetActualType(IBsonReader bsonReader, Type nominalType)
    {
        // the BsonReader is sitting at the value whose actual type needs to be found
        var bsonType = bsonReader.GetCurrentBsonType();
        if (bsonType == BsonType.Document)
        {
            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            var actualType = nominalType;
            if (bsonReader.FindElement("_t"))
            {
                var context = BsonDeserializationContext.CreateRoot(bsonReader);
                var discriminator = BsonValueSerializer.Instance.Deserialize(context);
                if (discriminator.IsBsonArray)
                {
                    discriminator = discriminator.AsBsonArray.Last(); // last item is leaf class discriminator
                }

                actualType = Type.GetType(discriminator.AsString);
            }
            bsonReader.ReturnToBookmark(bookmark);
            return actualType;
            
        }

        return nominalType;
    }

    public BsonValue GetDiscriminator(Type nominalType, Type actualType) => $"{actualType.FullName}, {actualType.Assembly.GetName().Name}";
}