/*using GoLive.Saturn.Data.Entities;
using LiteDB;

namespace Saturn.Data.LiteDb;

public static class LiteDBRefSerializer
{
    public static Func<object, BsonValue> Serialize(Type primitiveValueObjectType)
    {
        return (value) =>
        {
            if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Ref<>))
            {
                var idProperty = value.GetType().GetProperty("Id");
                var id = idProperty?.GetValue(value);
                if (id != null)
                {
                    return new BsonValue(id);
                }
            }

            throw new NotSupportedException($"Type {primitiveValueObjectType} is not supported for serialization.");
        };
    }

    public static Func<BsonValue, object> Deserialize(Type primitiveValueObjectType)
    {
        return (bson) =>
        {
            if (bson == null) return null;
            
            // Get the generic type Ref<T> where T is the primitiveValueObjectType
            var refType = typeof(Ref<>).MakeGenericType(primitiveValueObjectType);
        
            // Create a new instance of Ref<T>
            var instance = Activator.CreateInstance(refType);
            
            // Set the Id property
            var idProperty = refType.GetProperty("Id");
            idProperty?.SetValue(instance, bson.AsString);
            
            return instance;
        };
    }
}*/