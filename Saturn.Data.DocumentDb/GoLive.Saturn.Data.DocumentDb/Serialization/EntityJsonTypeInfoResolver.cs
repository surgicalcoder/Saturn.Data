using System.Text.Json.Serialization.Metadata;

namespace Saturn.Data.DocumentDb.Serialization;

public static class EntityJsonTypeInfoResolver
{
    private static readonly HashSet<string> IgnoredMembers = new(StringComparer.Ordinal)
    {
        "EnableChangeTracking",
        "Changes",
        "_shortId"
    };

    public static IJsonTypeInfoResolver Create(IJsonTypeInfoResolver additional = null)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Modify);

        return additional is null
            ? resolver
            : JsonTypeInfoResolver.Combine(resolver, additional);
    }

    private static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (IgnoredMembers.Contains(typeInfo.Properties[index].Name))
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }
}
