using System.Text.Json;
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.ChangeTracking;

public static class UpdateDocumentBuilder
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Build(IReadOnlyList<FieldChange> changes) => Build(changes, new VisibilityChangeSetFilter());

    public static string Build(IReadOnlyList<FieldChange> changes, IChangeSetFilter filter)
    {
        var set = new JsonObject();
        var unset = new JsonObject();
        var inc = new JsonObject();
        var addToSet = new JsonObject();
        var pull = new JsonObject();

        foreach (var change in OrderForApply(changes))
        {
            if (filter?.Filter(change) is null)
            {
                continue;
            }

            switch (change.Kind)
            {
                case ChangeKind.Set:
                    set[change.Path] = ToNode(change.NewValue);
                    break;
                case ChangeKind.Unset:
                    unset[change.Path] = true;
                    break;
                case ChangeKind.Increment:
                    inc[change.Path] = ToNode(change.NewValue);
                    break;
                case ChangeKind.ListClear:
                    set[change.Path] = new JsonArray();
                    break;
                case ChangeKind.ListAdd:
                    ApplyListAdd(change, set, addToSet);
                    break;
                case ChangeKind.ListRemove:
                    ApplyListRemove(change, set, unset, pull);
                    break;
                case ChangeKind.ListReplace:
                case ChangeKind.ListMove:
                    ApplyListReplace(change, set);
                    break;
            }
        }

        var document = new JsonObject();

        if (set.Count > 0)
        {
            document["$set"] = set;
        }

        if (unset.Count > 0)
        {
            document["$unset"] = unset;
        }

        if (inc.Count > 0)
        {
            document["$inc"] = inc;
        }

        if (addToSet.Count > 0)
        {
            document["$addToSet"] = addToSet;
        }

        if (pull.Count > 0)
        {
            document["$pull"] = pull;
        }

        return document.ToJsonString(Options);
    }

    private static void ApplyListAdd(FieldChange change, JsonObject set, JsonObject addToSet)
    {
        if (change.Strategy == CollectionStrategy.SetOps)
        {
            addToSet[change.Path] = ToNode(change.NewValue);
        }
        else if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            set[change.Path + "." + change.Index.Value] = ToNode(change.NewValue);
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static void ApplyListRemove(FieldChange change, JsonObject set, JsonObject unset, JsonObject pull)
    {
        if (change.Strategy == CollectionStrategy.SetOps)
        {
            pull[change.Path] = ToNode(change.OldValue);
        }
        else if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            unset[change.Path + "." + change.Index.Value] = true;
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static void ApplyListReplace(FieldChange change, JsonObject set)
    {
        if (change.Strategy == CollectionStrategy.IndexedOps && change.Index.HasValue)
        {
            set[change.Path + "." + change.Index.Value] = ToNode(change.NewValue);
        }
        else
        {
            set[change.Path] = ToNode(change.NewValue);
        }
    }

    private static IEnumerable<FieldChange> OrderForApply(IReadOnlyList<FieldChange> changes)
    {
        var removals = new List<FieldChange>();
        var others = new List<FieldChange>();

        foreach (var change in changes)
        {
            if (change.Kind == ChangeKind.ListRemove && change.Strategy == CollectionStrategy.IndexedOps)
            {
                removals.Add(change);
            }
            else
            {
                others.Add(change);
            }
        }

        removals.Sort((left, right) => (right.Index ?? 0).CompareTo(left.Index ?? 0));

        foreach (var change in others)
        {
            yield return change;
        }

        foreach (var change in removals)
        {
            yield return change;
        }
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        decimal number => JsonValue.Create(number),
        DateTime dateTime => JsonValue.Create(dateTime),
        DateTimeOffset dateTimeOffset => JsonValue.Create(dateTimeOffset),
        Enum enumeration => JsonValue.Create(enumeration.ToString()),
        Entity entity => JsonValue.Create(entity.Id),
        IEntityReference reference => JsonValue.Create(reference.RefId),
        _ => JsonSerializer.SerializeToNode(value, Options)
    };
}
