using GoLive.Saturn.Data.EntitySerializers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GoLive.Saturn.Data.Conventions
{
    public class IdMemberMapConvention : ConventionBase, IMemberMapConvention
    {
        public void Apply(BsonMemberMap memberMap)
        {
            if (memberMap.MemberName == "Id")
            {
                memberMap.SetSerializer(new EntityObjectIdSerializer());
            }

            return;
        }
    }
}