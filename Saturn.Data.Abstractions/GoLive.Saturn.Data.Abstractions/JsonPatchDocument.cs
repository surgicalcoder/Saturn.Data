using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GoLive.Saturn.Data.Abstractions;

public static class JsonPatchDocument
{
    public static bool HasOperators(JsonObject patch)
        => patch.ContainsKey("$set")
           || patch.ContainsKey("$unset")
           || patch.ContainsKey("$inc")
           || patch.ContainsKey("$addToSet")
           || patch.ContainsKey("$pull");

    public static void Apply(JsonObject document, JsonObject patch)
    {
        if (patch.TryGetPropertyValue("$set", out var setNode) && setNode is JsonObject set)
        {
            foreach (var property in set)
            {
                SetPath(document, property.Key, property.Value?.DeepClone());
            }
        }

        if (patch.TryGetPropertyValue("$unset", out var unsetNode) && unsetNode is JsonObject unset)
        {
            foreach (var property in unset)
            {
                RemovePath(document, property.Key);
            }
        }

        if (patch.TryGetPropertyValue("$inc", out var incNode) && incNode is JsonObject increment)
        {
            foreach (var property in increment)
            {
                IncrementPath(document, property.Key, property.Value);
            }
        }

        if (patch.TryGetPropertyValue("$addToSet", out var addNode) && addNode is JsonObject addToSet)
        {
            foreach (var property in addToSet)
            {
                AddToSet(document, property.Key, property.Value?.DeepClone());
            }
        }

        if (patch.TryGetPropertyValue("$pull", out var pullNode) && pullNode is JsonObject pull)
        {
            foreach (var property in pull)
            {
                Pull(document, property.Key, property.Value);
            }
        }
    }

    private static void SetPath(JsonObject root, string path, JsonNode? value)
    {
        if (!TryResolveParent(root, path, create: true, out var parent, out var leaf))
        {
            return;
        }

        Assign(parent, leaf, value);
    }

    private static void RemovePath(JsonObject root, string path)
    {
        if (!TryResolveParent(root, path, create: false, out var parent, out var leaf))
        {
            return;
        }

        if (parent is JsonObject obj)
        {
            obj.Remove(leaf);
        }
        else if (parent is JsonArray array && int.TryParse(leaf, out var index) && index >= 0 && index < array.Count)
        {
            array.RemoveAt(index);
        }
    }

    private static void IncrementPath(JsonObject root, string path, JsonNode? delta)
    {
        if (!TryResolveParent(root, path, create: true, out var parent, out var leaf))
        {
            return;
        }

        var current = TryRead(parent, leaf);
        var result = (current?.GetValue<decimal>() ?? 0m) + (delta?.GetValue<decimal>() ?? 0m);
        Assign(parent, leaf, JsonValue.Create(result));
    }

    private static void AddToSet(JsonObject root, string path, JsonNode? value)
    {
        if (!TryResolveParent(root, path, create: true, out var parent, out var leaf))
        {
            return;
        }

        var current = TryRead(parent, leaf);

        if (current is not JsonArray array)
        {
            array = new JsonArray();
            Assign(parent, leaf, array);
        }

        foreach (var existing in array)
        {
            if (JsonNode.DeepEquals(existing, value))
            {
                return;
            }
        }

        array.Add(value);
    }

    private static void Pull(JsonObject root, string path, JsonNode? value)
    {
        if (!TryResolveParent(root, path, create: false, out var parent, out var leaf))
        {
            return;
        }

        if (TryRead(parent, leaf) is not JsonArray array)
        {
            return;
        }

        for (var index = array.Count - 1; index >= 0; index--)
        {
            if (JsonNode.DeepEquals(array[index], value))
            {
                array.RemoveAt(index);
            }
        }
    }

    private static void Assign(JsonNode parent, string leaf, JsonNode? value)
    {
        if (parent is JsonObject obj)
        {
            obj[leaf] = value;
        }
        else if (parent is JsonArray array && int.TryParse(leaf, out var index) && index >= 0)
        {
            while (array.Count <= index)
            {
                array.Add(null);
            }

            array[index] = value;
        }
    }

    private static JsonNode? TryRead(JsonNode parent, string leaf)
        => parent switch
        {
            JsonObject obj => obj[leaf],
            JsonArray array when int.TryParse(leaf, out var index) && index >= 0 && index < array.Count => array[index],
            _ => null
        };

    private static bool TryResolveParent(JsonObject root, string path, bool create, out JsonNode parent, out string leaf)
    {
        parent = root;
        leaf = path;

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (!path.Contains('.'))
        {
            return true;
        }

        var segments = path.Split('.');
        JsonNode current = root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            var nextIsIndex = int.TryParse(segments[index + 1], out _);

            if (current is JsonObject obj)
            {
                if (obj[segment] is { } existing)
                {
                    current = existing;
                    continue;
                }

                if (!create)
                {
                    return false;
                }

                var created = nextIsIndex ? (JsonNode)new JsonArray() : new JsonObject();
                obj[segment] = created;
                current = created;
            }
            else if (current is JsonArray array && int.TryParse(segment, out var position) && position >= 0)
            {
                while (array.Count <= position)
                {
                    if (!create)
                    {
                        return false;
                    }

                    array.Add(nextIsIndex ? (JsonNode)new JsonArray() : new JsonObject());
                }

                if (array[position] is not { } element)
                {
                    if (!create)
                    {
                        return false;
                    }

                    element = nextIsIndex ? (JsonNode)new JsonArray() : new JsonObject();
                    array[position] = element;
                }

                current = element;
            }
            else
            {
                return false;
            }
        }

        parent = current;
        leaf = segments[^1];
        return true;
    }
}
