using System.Reflection;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Data.Entities.Cascade;

namespace Saturn.Data.DocumentDb.Cascade;

internal sealed record CascadeRelation(Type ChildType, CascadeMode Mode, CascadeDepth Depth, SharedScopePolicy SharedScope, string RefPath);

internal static class CascadeRelationResolver
{
    private static readonly Lazy<IReadOnlyDictionary<Type, IReadOnlyList<CascadeRelation>>> Relations =
        new(BuildAllRelations, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<Type, IReadOnlyList<string>>> Paths =
        new(BuildAllPaths, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<CascadeRelation> RelationsForParent(Type parentType)
        => Relations.Value.TryGetValue(parentType, out var relations) ? relations : Array.Empty<CascadeRelation>();

    public static IReadOnlyList<string> RefPathsForChild(Type childType)
        => Paths.Value.TryGetValue(childType, out var paths) ? paths : Array.Empty<string>();

    public static string PathExpression(string refPath) => refPath switch
    {
        "Scope" => "_scope",
        "SecondScope" => "_scope2",
        _ => $"json_extract(_doc, '$.{refPath}')"
    };

    private static IReadOnlyDictionary<Type, IReadOnlyList<CascadeRelation>> BuildAllRelations()
    {
        var map = new Dictionary<Type, List<CascadeRelation>>();

        foreach (var childType in EnumerateEntityTypes())
        {
            foreach (var property in childType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<CascadeDeleteAttribute>();
                var parentType = attribute is null ? null : RefTarget(property.PropertyType);

                if (attribute is null || parentType is null)
                {
                    continue;
                }

                Add(map, parentType, new CascadeRelation(childType, attribute.Mode, attribute.Depth, attribute.SharedScope, property.Name));
            }

            var scopeAttribute = childType.GetCustomAttribute<CascadeDeleteOnScopeAttribute>();

            if (scopeAttribute is null)
            {
                continue;
            }

            var scopeProperty = childType.GetProperty("Scope", BindingFlags.Public | BindingFlags.Instance);
            var scopeTarget = scopeProperty is null ? null : RefTarget(scopeProperty.PropertyType);

            if (scopeTarget is not null)
            {
                Add(map, scopeTarget, new CascadeRelation(childType, scopeAttribute.Mode, scopeAttribute.Depth, scopeAttribute.SharedScope, "Scope"));
            }
        }

        return map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CascadeRelation>)pair.Value.Distinct().ToList());
    }

    private static IReadOnlyDictionary<Type, IReadOnlyList<string>> BuildAllPaths()
    {
        var map = new Dictionary<Type, IReadOnlyList<string>>();

        foreach (var childType in EnumerateEntityTypes())
        {
            var paths = new List<string>();

            foreach (var property in childType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<CascadeDeleteAttribute>() is not null)
                {
                    paths.Add(property.Name);
                }
            }

            if (childType.GetCustomAttribute<CascadeDeleteOnScopeAttribute>() is not null)
            {
                paths.Add("Scope");
            }

            if (paths.Count > 0)
            {
                map[childType] = paths.Distinct(StringComparer.Ordinal).ToList();
            }
        }

        return map;
    }

    private static void Add(Dictionary<Type, List<CascadeRelation>> map, Type parentType, CascadeRelation relation)
    {
        if (!map.TryGetValue(parentType, out var list))
        {
            list = new List<CascadeRelation>();
            map[parentType] = list;
        }

        list.Add(relation);
    }

    private static IEnumerable<Type> EnumerateEntityTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type is not null).ToArray()!;
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type is { IsClass: true, IsAbstract: false } && typeof(Entity).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }

    private static Type? RefTarget(Type type)
    {
        if (type == typeof(WeakRef))
        {
            return null;
        }

        if (!type.IsGenericType)
        {
            return null;
        }

        var definition = type.GetGenericTypeDefinition();

        return definition == typeof(Ref<>) || definition == typeof(WeakRef<>)
            ? type.GenericTypeArguments[0]
            : null;
    }
}
