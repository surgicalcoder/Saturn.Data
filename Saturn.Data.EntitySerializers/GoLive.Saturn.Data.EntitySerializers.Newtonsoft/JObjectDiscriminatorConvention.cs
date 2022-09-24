using System;
using System.Dynamic;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GoLive.Saturn.Data.EntitySerializers.Newtonsoft
{
    public class JObjectDiscriminatorConvention : IDiscriminatorConvention
    {
        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            return typeof(ExpandoObject);
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            return null;
        }

        public string ElementName => null;
    }
}
