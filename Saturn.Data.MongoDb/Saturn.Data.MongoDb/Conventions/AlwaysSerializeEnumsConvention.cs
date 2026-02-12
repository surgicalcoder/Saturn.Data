using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace Saturn.Data.MongoDb.Conventions;

/// <summary>
/// Convention that ensures enum properties are always serialized, even when they have default values.
/// This prevents issues where queries for default enum values fail because the field is not stored in the database.
/// Must be registered AFTER IgnoreIfDefaultConvention to override its behavior for enums.
/// </summary>
public class AlwaysSerializeEnumsConvention : ConventionBase, IMemberMapConvention
{
    public void Apply(BsonMemberMap memberMap)
    {
        var memberType = memberMap.MemberType;
        
        // Check if the member type is an enum or a nullable enum
        var isEnum = memberType.IsEnum;
        var isNullableEnum = Nullable.GetUnderlyingType(memberType)?.IsEnum ?? false;
        
        if (isEnum || isNullableEnum)
        {
            // Override any previous ShouldSerialize settings (like from IgnoreIfDefaultConvention)
            // by explicitly setting it to always return true for enums
            memberMap.SetShouldSerializeMethod(_ => true);
            
            // Also ensure we don't ignore default values for this member
            memberMap.SetIgnoreIfDefault(false);
        }
    }
}

