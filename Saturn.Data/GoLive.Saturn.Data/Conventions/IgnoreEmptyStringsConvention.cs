using System;
using System.Net;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GoLive.Saturn.Data.Conventions;

public class IgnoreEmptyStringsConvention : ConventionBase, IMemberMapConvention
{
    public void Apply(BsonMemberMap memberMap)
    {
        if (memberMap.ElementName == "Id")
        {
            return;
        }
            
        if (memberMap.MemberType == typeof(string))
        {
            memberMap.SetShouldSerializeMethod(o => !string.IsNullOrWhiteSpace(memberMap.Getter(o) as string));
        }
    }
}