using System.Reflection;
using GoLive.Saturn.Data.Entities;
using LiteDB;

namespace Saturn.Data.LiteDb;

public class CustomEntityMapper : BsonMapper
{
    protected override IEnumerable<MemberInfo> GetTypeMembers(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ref<>))
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name == nameof(GoLive.Saturn.Data.Entities.Entity.Id));
        }
        
        return base.GetTypeMembers(type)
            .Where(m => !m.IsDefined(typeof(NonSerializedAttribute), true) &&
                        m.Name != "_shortId" &&
                        m.Name != "Changes" &&
                        m.Name != "EnableChangeTracking");
    }
}

