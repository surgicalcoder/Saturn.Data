using System.Reflection;
using LiteDB;

namespace Saturn.Data.LiteDb;

public class EntityMapper : BsonMapper
{
    protected override IEnumerable<MemberInfo> GetTypeMembers(Type type)
    {
        return base.GetTypeMembers(type)
            .Where(m => !m.IsDefined(typeof(NonSerializedAttribute), true) &&
                        m.Name != "_shortId" &&
                        m.Name != "Changes" &&
                        m.Name != "EnableChangeTracking");
    }
}